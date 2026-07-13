using LIAnsureProtect.Application;
using LIAnsureProtect.Infrastructure;
using LIAnsureProtect.Infrastructure.Caching;
using LIAnsureProtect.Infrastructure.Documents;
using LIAnsureProtect.Modules.Claims.Infrastructure;
using LIAnsureProtect.Modules.Notifications.Infrastructure;
using LIAnsureProtect.Modules.Quoting.Infrastructure;
using LIAnsureProtect.Modules.Underwriting.Infrastructure;
using LIAnsureProtect.Platform;
using LIAnsureProtect.Platform.Abstractions.Security;
using LIAnsureProtect.Worker;


var builder = Host.CreateApplicationBuilder(args);
var databaseConnectionString = builder.Configuration.GetConnectionString("LIAnsureProtect");

// Platform:Profile selects the Local <-> AWS adapter set (see the API host for the same pattern).
var platformProfile = PlatformProfileResolver.Resolve(builder.Configuration);

if (builder.Environment.IsProduction()
    || platformProfile == LIAnsureProtect.Platform.Abstractions.PlatformProfile.Aws)
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
}

builder.Services.AddPlatform(builder.Configuration);
builder.Services.AddNotificationsModule(databaseConnectionString, platformProfile);
builder.Services.AddQuotingModule();
builder.Services.AddUnderwritingModule(databaseConnectionString, platformProfile);
builder.Services.AddClaimsModule(databaseConnectionString, platformProfile);
builder.Services.AddApplication();
builder.Services.Configure<DocumentStorageOptions>(builder.Configuration.GetSection("DocumentStorage"));
builder.Services.Configure<NotificationPublisherOptions>(builder.Configuration.GetSection("Notifications"));
builder.Services.Configure<CacheOptions>(builder.Configuration.GetSection("Cache"));
builder.Services.AddInfrastructure(databaseConnectionString, platformProfile);
builder.Services.AddScoped<ICurrentUser, WorkerCurrentUser>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

await host.RunAsync();
