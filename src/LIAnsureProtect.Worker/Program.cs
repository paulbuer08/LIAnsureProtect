using LIAnsureProtect.Application;
using LIAnsureProtect.Infrastructure;
using LIAnsureProtect.Infrastructure.Documents;
using LIAnsureProtect.Platform;
using LIAnsureProtect.Worker;


var builder = Host.CreateApplicationBuilder(args);
var databaseConnectionString = builder.Configuration.GetConnectionString("LIAnsureProtect");

// Platform:Profile selects the Local <-> AWS adapter set (see the API host for the same pattern).
var platformProfile = PlatformProfileResolver.Resolve(builder.Configuration);

builder.Services.AddPlatform(builder.Configuration);
builder.Services.AddApplication();
builder.Services.Configure<DocumentStorageOptions>(builder.Configuration.GetSection("DocumentStorage"));
builder.Services.AddInfrastructure(databaseConnectionString, platformProfile);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

await host.RunAsync();
