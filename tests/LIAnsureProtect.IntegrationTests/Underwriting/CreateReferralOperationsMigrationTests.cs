using LIAnsureProtect.Modules.Underwriting.Infrastructure;
using LIAnsureProtect.Modules.Underwriting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace LIAnsureProtect.IntegrationTests.Underwriting;

public sealed class CreateReferralOperationsMigrationTests
{
    [Fact]
    public void UnderwritingModuleMigrationsCreateReferralOperationsTables()
    {
        // Arrange — mirrors UnderwritingModuleMigrationsCreateUnderwritingSchemaAndAiReviews in
        // DependencyRegistrationTests: build the service container, resolve UnderwritingDbContext, and
        // generate the full SQL script without running against a live database.
        var services = new ServiceCollection();
        services.AddUnderwritingModule("Host=localhost;Database=liansureprotect_test;Username=postgres;Password=postgres");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<UnderwritingDbContext>();
        var migrator = dbContext.GetInfrastructure().GetRequiredService<IMigrator>();

        // Act
        var script = migrator.GenerateScript();

        // Assert: the four referral tables and the dedupe table all land in the "underwriting" schema.
        Assert.Contains("CREATE TABLE underwriting.quote_referral_operations", script);
        Assert.Contains("ux_quote_referral_operations_quote_id", script);
        Assert.Contains("ix_quote_referral_operations_status_priority_due_at_utc", script);

        Assert.Contains("CREATE TABLE underwriting.quote_referral_work_notes", script);
        Assert.Contains("ix_quote_referral_work_notes_quote_id_created_at_utc", script);

        Assert.Contains("CREATE TABLE underwriting.quote_referral_follow_up_tasks", script);
        Assert.Contains("ix_quote_referral_follow_up_tasks_quote_id_completed_due_at_utc", script);

        Assert.Contains("CREATE TABLE underwriting.quote_referral_timeline_entries", script);
        Assert.Contains("ix_quote_referral_timeline_entries_quote_id_created_at_utc", script);

        Assert.Contains("CREATE TABLE underwriting.referral_operation_projected_messages", script);

        // Confirm no cross-context FK to quotes or submissions tables.
        Assert.DoesNotContain("REFERENCES public.quotes", script);
        Assert.DoesNotContain("REFERENCES public.submissions", script);
        Assert.DoesNotContain("REFERENCES quotes", script);
        Assert.DoesNotContain("REFERENCES submissions", script);
    }
}
