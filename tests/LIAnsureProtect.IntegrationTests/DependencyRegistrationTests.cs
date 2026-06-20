using LIAnsureProtect.Application;
using LIAnsureProtect.Application.Common.Idempotency;
using LIAnsureProtect.Application.Common.Persistence;
using LIAnsureProtect.Application.Submissions;
using LIAnsureProtect.Infrastructure;
using LIAnsureProtect.Infrastructure.Persistence;
using LIAnsureProtect.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace LIAnsureProtect.IntegrationTests;


public sealed class DependencyRegistrationTests
{
    [Fact]
    public void ApplicationAndInfrastructureRegistrationCanBeComposed()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddApplication();
        services.AddInfrastructure("Host=localhost;Database=liansureprotect_test;Username=postgres;Password=postgres");
        using var provider = services.BuildServiceProvider();

        // Assert
        Assert.NotNull(provider);
    }

    [Fact]
    public void InfrastructureRegistrationProvidesPersistenceServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddInfrastructure("Host=localhost;Database=liansureprotect_test;Username=postgres;Password=postgres");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        // Assert
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<SubmissionDbContext>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISubmissionRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IUnitOfWork>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IIdempotencyService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IOutboxDispatcher>());
    }

    [Fact]
    public void PersistenceMigrationsCreatePgvectorExtensionAndSubmissionsTable()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddInfrastructure("Host=localhost;Database=liansureprotect_test;Username=postgres;Password=postgres");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        var migrator = dbContext.GetInfrastructure().GetRequiredService<IMigrator>();

        // Act
        var script = migrator.GenerateScript();

        // Assert
        Assert.Contains("CREATE EXTENSION IF NOT EXISTS vector", script);
        Assert.Contains("CREATE TABLE submissions", script);
        Assert.Contains("CREATE TABLE outbox_messages", script);
        Assert.Contains("ix_outbox_messages_processed_at_utc_created_at_utc", script);
        Assert.Contains("CREATE TABLE idempotency_records", script);
        Assert.Contains("ux_idempotency_records_key", script);
    }
}
