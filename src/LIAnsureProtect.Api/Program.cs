using LIAnsureProtect.Application;
using LIAnsureProtect.Infrastructure;


var applicationName = typeof(Program).Assembly.GetName().Name ?? "LIAnsureProtect.Api";
var builder = WebApplication.CreateBuilder(args);


// 1) Add services to the container.
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddControllers();

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
