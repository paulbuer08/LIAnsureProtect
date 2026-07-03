using LIAnsureProtect.Modules.Claims.Infrastructure;
using LIAnsureProtect.Modules.Claims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace LIAnsureProtect.IntegrationTests.Claims;

public sealed class CreateClaimsSchemaMigrationTests
{
    [Fact]
    public void ClaimsModuleMigrationsCreateClaimsSchemaTablesAndOutbox()
    {
        // Arrange — build the module's service container, resolve ClaimsDbContext, and generate the
        // full SQL script without running against a live database (same pattern as the
        // Underwriting migration tests).
        var services = new ServiceCollection();
        services.AddClaimsModule("Host=localhost;Database=liansureprotect_test;Username=postgres;Password=postgres");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClaimsDbContext>();
        var migrator = dbContext.GetInfrastructure().GetRequiredService<IMigrator>();

        // Act
        var script = migrator.GenerateScript();

        // Assert: the claim tables and the module outbox all land in the "claims" schema.
        Assert.Contains("CREATE TABLE claims.claims", script);
        Assert.Contains("ux_claims_claim_number", script);
        Assert.Contains("ix_claims_owner_user_id", script);
        Assert.Contains("ix_claims_policy_id", script);
        Assert.Contains("ix_claims_status_filed_at_utc", script);

        Assert.Contains("CREATE TABLE claims.claim_timeline_entries", script);
        Assert.Contains("ix_claim_timeline_entries_claim_id_created_at_utc", script);

        Assert.Contains("CREATE TABLE claims.outbox_messages", script);

        // The migrations history table lives inside the module's own schema.
        Assert.Contains("claims.\"__EFMigrationsHistory\"", script);

        // Confirm no cross-context FK to policy or submission tables — the policy is id-only.
        Assert.DoesNotContain("REFERENCES public.policies", script);
        Assert.DoesNotContain("REFERENCES policies", script);
        Assert.DoesNotContain("REFERENCES public.submissions", script);
        Assert.DoesNotContain("REFERENCES submissions", script);
    }
}
