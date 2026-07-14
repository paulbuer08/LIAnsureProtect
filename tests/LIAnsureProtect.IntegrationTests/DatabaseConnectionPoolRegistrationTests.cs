using LIAnsureProtect.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

namespace LIAnsureProtect.IntegrationTests;

public sealed class DatabaseConnectionPoolRegistrationTests
{
    [Fact]
    public void Registers_One_Explicitly_Governed_DataSource_Per_Host()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionPool:MinimumPoolSize"] = "2",
                ["Database:ConnectionPool:MaximumPoolSize"] = "17",
                ["Database:ConnectionPool:ConnectionTimeoutSeconds"] = "11",
                ["Database:ConnectionPool:CommandTimeoutSeconds"] = "23",
                ["Database:ConnectionPool:IdleLifetimeSeconds"] = "180",
                ["Database:ConnectionPool:PruningIntervalSeconds"] = "9",
                ["Database:ConnectionPool:ConnectionLifetimeSeconds"] = "900"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddPostgreSqlDataSource(
            configuration,
            "Host=localhost;Database=liansureprotect;Username=postgres;Password=postgres",
            "LIAnsureProtect.TestHost");

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<NpgsqlDataSource>();
        var second = provider.GetRequiredService<NpgsqlDataSource>();
        var settings = new NpgsqlConnectionStringBuilder(first.ConnectionString);

        Assert.Same(first, second);
        Assert.True(settings.Pooling);
        Assert.Equal(2, settings.MinPoolSize);
        Assert.Equal(17, settings.MaxPoolSize);
        Assert.Equal(11, settings.Timeout);
        Assert.Equal(23, settings.CommandTimeout);
        Assert.Equal(180, settings.ConnectionIdleLifetime);
        Assert.Equal(9, settings.ConnectionPruningInterval);
        Assert.Equal(900, settings.ConnectionLifetime);
        Assert.Equal("LIAnsureProtect.TestHost", settings.ApplicationName);
    }

    [Fact]
    public void Rejects_A_Minimum_That_Exceeds_The_Maximum()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionPool:MinimumPoolSize"] = "12",
                ["Database:ConnectionPool:MaximumPoolSize"] = "4"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddPostgreSqlDataSource(
            configuration,
            "Host=localhost;Database=liansureprotect;Username=postgres;Password=postgres",
            "LIAnsureProtect.TestHost");

        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<NpgsqlDataSource>());
    }
}
