using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

namespace LIAnsureProtect.Infrastructure.Persistence;

public static class PostgreSqlDataSourceRegistration
{
    /// <summary>
    /// Registers one explicitly governed Npgsql pool for every DbContext in this host process.
    /// The API and Worker use separate process-level limits configured in their own appsettings.
    /// </summary>
    public static IServiceCollection AddPostgreSqlDataSource(
        this IServiceCollection services,
        IConfiguration configuration,
        string? databaseConnectionString,
        string applicationName)
    {
        if (string.IsNullOrWhiteSpace(databaseConnectionString))
            throw new InvalidOperationException("Connection string 'LIAnsureProtect' is required.");

        services.AddOptions<DatabaseConnectionPoolOptions>()
            .Bind(configuration.GetSection(DatabaseConnectionPoolOptions.SectionName))
            .Validate(options => options.MinimumPoolSize >= 0,
                "Database:ConnectionPool:MinimumPoolSize cannot be negative.")
            .Validate(options => options.MaximumPoolSize > 0,
                "Database:ConnectionPool:MaximumPoolSize must be greater than zero.")
            .Validate(options => options.MinimumPoolSize <= options.MaximumPoolSize,
                "Database:ConnectionPool:MinimumPoolSize cannot exceed MaximumPoolSize.")
            .Validate(options => options.ConnectionTimeoutSeconds > 0,
                "Database:ConnectionPool:ConnectionTimeoutSeconds must be greater than zero.")
            .Validate(options => options.CommandTimeoutSeconds > 0,
                "Database:ConnectionPool:CommandTimeoutSeconds must be greater than zero.")
            .Validate(options => options.IdleLifetimeSeconds >= 0,
                "Database:ConnectionPool:IdleLifetimeSeconds cannot be negative.")
            .Validate(options => options.PruningIntervalSeconds > 0,
                "Database:ConnectionPool:PruningIntervalSeconds must be greater than zero.")
            .Validate(options => options.ConnectionLifetimeSeconds >= 0,
                "Database:ConnectionPool:ConnectionLifetimeSeconds cannot be negative.")
            .ValidateOnStart();

        services.AddSingleton(serviceProvider =>
        {
            var pool = serviceProvider.GetRequiredService<IOptions<DatabaseConnectionPoolOptions>>().Value;
            var connectionString = new NpgsqlConnectionStringBuilder(databaseConnectionString)
            {
                Pooling = true,
                MinPoolSize = pool.MinimumPoolSize,
                MaxPoolSize = pool.MaximumPoolSize,
                Timeout = pool.ConnectionTimeoutSeconds,
                CommandTimeout = pool.CommandTimeoutSeconds,
                ConnectionIdleLifetime = pool.IdleLifetimeSeconds,
                ConnectionPruningInterval = pool.PruningIntervalSeconds,
                ConnectionLifetime = pool.ConnectionLifetimeSeconds,
                ApplicationName = applicationName
            };

            return new NpgsqlDataSourceBuilder(connectionString.ConnectionString).Build();
        });

        return services;
    }
}
