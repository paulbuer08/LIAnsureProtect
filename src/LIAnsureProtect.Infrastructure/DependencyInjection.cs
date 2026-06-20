using LIAnsureProtect.Application.Common.Persistence;
using LIAnsureProtect.Application.Submissions;
using LIAnsureProtect.Infrastructure.Persistence;
using LIAnsureProtect.Infrastructure.Persistence.Outbox;
using LIAnsureProtect.Infrastructure.Submissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LIAnsureProtect.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string? databaseConnectionString)
    {
        if (string.IsNullOrWhiteSpace(databaseConnectionString))
            throw new InvalidOperationException("Connection string 'LIAnsureProtect' is required.");

        services.AddDbContext<SubmissionDbContext>(options =>
        {
            options.UseNpgsql(databaseConnectionString);
        });

        services.AddScoped<ISubmissionRepository, EfCoreSubmissionRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();
        services.AddScoped<IOutboxDispatcher, OutboxDispatcher>();

        return services;
    }
}
