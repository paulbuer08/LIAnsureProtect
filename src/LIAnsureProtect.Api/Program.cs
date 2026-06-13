using LIAnsureProtect.Application;
using LIAnsureProtect.Application.Common.Security;
using LIAnsureProtect.Api.Security;
using LIAnsureProtect.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;


var applicationName = typeof(Program).Assembly.GetName().Name ?? "LIAnsureProtect.Api";
var builder = WebApplication.CreateBuilder(args);


// 1) Add services to the container.
var databaseConnectionString = builder.Configuration.GetConnectionString("LIAnsureProtect");

builder.Services.AddApplication();
builder.Services.AddInfrastructure(databaseConnectionString);
builder.Services.AddControllers();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

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

builder.Services.AddAuthorization(AuthorizationPolicies.AddApplicationAuthorizationPolicies);


// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

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

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new
{
    application = applicationName,
    status = "Running"
}));

app.MapHealthChecks("/api/v1/health");
app.MapControllers();


// --------------------------------------------------------------------------------
// 3) Run the app
await app.RunAsync();
