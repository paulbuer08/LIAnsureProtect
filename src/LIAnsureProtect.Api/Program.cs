using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using LIAnsureProtect.Application;
using LIAnsureProtect.Api.Observability;
using LIAnsureProtect.Platform.Abstractions.Security;
using LIAnsureProtect.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using LIAnsureProtect.Infrastructure;
using LIAnsureProtect.Infrastructure.Caching;
using LIAnsureProtect.Infrastructure.Documents;
using LIAnsureProtect.Infrastructure.Persistence;
using LIAnsureProtect.Modules.Notifications.Infrastructure;
using LIAnsureProtect.Modules.Notifications.Infrastructure.Persistence;
using LIAnsureProtect.Modules.Quoting.Infrastructure;
using LIAnsureProtect.Modules.Underwriting.Infrastructure;
using LIAnsureProtect.Modules.Underwriting.Infrastructure.Persistence;
using LIAnsureProtect.Platform;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;


var applicationName = typeof(Program).Assembly.GetName().Name ?? "LIAnsureProtect.Api";
var builder = WebApplication.CreateBuilder(args);
const string LocalFrontendCorsPolicy = "LocalFrontend";


// 1) Add services to the container.
var databaseConnectionString = builder.Configuration.GetConnectionString("LIAnsureProtect");

// Platform:Profile selects the Local <-> AWS adapter set. Resolve it once and pass it into the
// layer/module registrations so they wire the matching adapters (e.g. document storage).
var platformProfile = PlatformProfileResolver.Resolve(builder.Configuration);

builder.Services.AddPlatform(builder.Configuration);
builder.Services.AddNotificationsModule(databaseConnectionString, platformProfile);
builder.Services.AddQuotingModule();
builder.Services.AddUnderwritingModule(databaseConnectionString, platformProfile);
builder.Services.AddApplication();
builder.Services.Configure<DocumentStorageOptions>(builder.Configuration.GetSection("DocumentStorage"));
builder.Services.Configure<NotificationPublisherOptions>(builder.Configuration.GetSection("Notifications"));
builder.Services.Configure<CacheOptions>(builder.Configuration.GetSection("Cache"));
builder.Services.AddInfrastructure(databaseConnectionString, platformProfile);
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

var allowedCorsOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy(LocalFrontendCorsPolicy, policy =>
    {
        policy
            .WithOrigins(allowedCorsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// ICurrentUser lets use cases ask who is calling w/out depending on ASP.NET Core.
// HttpContextCurrentUser reads the current HTTP user's claims.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

// JWT bearer auth checks the caller's Auth0 access token.
// The API trusts only the configured issuer, audience, expiry, signature, and role claim.
var authenticationSection = builder.Configuration.GetSection("Authentication");
var authority = authenticationSection["Authority"];
var audience = authenticationSection["Audience"];
var roleClaimType = authenticationSection["RoleClaimType"] ?? "roles";

if (string.IsNullOrWhiteSpace(authority))
    throw new InvalidOperationException("Authentication:Authority is required.");

if (string.IsNullOrWhiteSpace(audience))
    throw new InvalidOperationException("Authentication:Audience is required.");

if (!Uri.TryCreate(authority, UriKind.Absolute, out var authorityUri) || authorityUri.Scheme != Uri.UriSchemeHttps)
    throw new InvalidOperationException("Authentication:Authority must be an absolute HTTPS URL.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.Audience = audience;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            RoleClaimType = roleClaimType
        };
    });

// Authorization policies decide w/c authenticated roles can enter each protected endpoint.
builder.Services.AddAuthorization(AuthorizationPolicies.AddApplicationAuthorizationPolicies);

// Rate limiting: a global fixed-window limiter partitioned per authenticated user (client IP
// fallback), with a stricter window for unsafe (write) methods. Limits are config-driven with
// generous defaults so only genuine floods are limited; production tightens them via
// RateLimiting:* configuration/environment. Limits are read from options per request so
// configuration (and test overrides) applied after registration are honored.
builder.Services.Configure<RateLimitingOptions>(builder.Configuration.GetSection("RateLimiting"));

builder.Services.AddRateLimiter(rateLimiterOptions =>
{
    rateLimiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    rateLimiterOptions.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var limits = context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
        var isUnsafe = HttpMethods.IsPost(context.Request.Method)
            || HttpMethods.IsPut(context.Request.Method)
            || HttpMethods.IsPatch(context.Request.Method)
            || HttpMethods.IsDelete(context.Request.Method);

        var callerKey = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(
            $"{(isUnsafe ? "unsafe" : "safe")}:{callerKey}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = isUnsafe ? limits.UnsafePermitLimit : limits.SafePermitLimit,
                Window = TimeSpan.FromSeconds(limits.WindowSeconds),
                QueueLimit = 0
            });
    });
    rateLimiterOptions.OnRejected = async (context, cancellationToken) =>
    {
        var limits = context.HttpContext.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
        context.HttpContext.Response.Headers.RetryAfter = limits.WindowSeconds.ToString();
        await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests.",
            Detail = "Rate limit exceeded. Please retry after a short wait."
        }, cancellationToken);
    };
});


// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services
    .AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<DbContextHealthCheck<SubmissionDbContext>>("submission-db", tags: ["ready", "database"])
    .AddCheck<DbContextHealthCheck<NotificationsDbContext>>("notifications-db", tags: ["ready", "database"])
    .AddCheck<DbContextHealthCheck<UnderwritingDbContext>>("underwriting-db", tags: ["ready", "database"]);

var app = builder.Build();
if (app.Logger.IsEnabled(LogLevel.Information))
{
    app.Logger.LogInformation("Starting {Application} in {Environment} mode.", applicationName, app.Environment.EnvironmentName);
}


// --------------------------------------------------------------------------------
// 2) Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseExceptionHandler();
}

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseHttpsRedirection();
app.UseCors(LocalFrontendCorsPolicy);
app.UseMiddleware<RequestCorrelationMiddleware>();
app.UseAuthentication();
// After authentication so the limiter can partition by the authenticated user; before
// authorization so a flood is shed with 429 rather than doing per-request authorization work.
app.UseRateLimiter();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new
{
    application = applicationName,
    status = "Running"
}));

app.MapHealthChecks("/api/v1/health", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});
app.MapHealthChecks("/api/v1/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});
app.MapHealthChecks("/api/v1/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
app.MapControllers();


// --------------------------------------------------------------------------------
// 3) Run the app
await app.RunAsync();
