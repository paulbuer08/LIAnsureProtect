using LIAnsureProtect.Application;
using LIAnsureProtect.Infrastructure;
using LIAnsureProtect.Worker;


var builder = Host.CreateApplicationBuilder(args);
var databaseConnectionString = builder.Configuration.GetConnectionString("LIAnsureProtect");

builder.Services.AddApplication();
builder.Services.AddInfrastructure(databaseConnectionString);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

await host.RunAsync();
