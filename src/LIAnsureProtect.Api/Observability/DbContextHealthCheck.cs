using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LIAnsureProtect.Api.Observability;

public sealed class DbContextHealthCheck<TContext>(IServiceScopeFactory scopeFactory) : IHealthCheck
    where TContext : DbContext
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

            return canConnect
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"{typeof(TContext).Name} cannot connect.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy($"{typeof(TContext).Name} readiness check failed.", exception);
        }
    }
}
