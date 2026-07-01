using LIAnsureProtect.Modules.Underwriting.Infrastructure;
using LIAnsureProtect.Modules.Underwriting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace LIAnsureProtect.IntegrationTests.Underwriting;

public sealed class CreateEvidenceRequestsMigrationTests
{
    [Fact]
    public void UnderwritingModuleMigrationsCreateEvidenceRequestTablesAndOutbox()
    {
        var services = new ServiceCollection();
        services.AddUnderwritingModule("Host=localhost;Database=liansureprotect_test;Username=postgres;Password=postgres");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<UnderwritingDbContext>();
        var migrator = dbContext.GetInfrastructure().GetRequiredService<IMigrator>();

        var script = migrator.GenerateScript();

        Assert.Contains("CREATE TABLE underwriting.outbox_messages", script);
        Assert.Contains("ix_outbox_messages_processed_at_utc_created_at_utc", script);
        Assert.Contains("ix_outbox_messages_dispatch_retry", script);

        Assert.Contains("CREATE TABLE underwriting.quote_evidence_requests", script);
        Assert.Contains("ix_quote_evidence_requests_owner_status_due_at_utc", script);
        Assert.Contains("ix_quote_evidence_requests_quote_status_updated_at_utc", script);
        Assert.DoesNotContain(
            "quote_referral_operation_id",
            Section(
                script,
                "CREATE TABLE underwriting.quote_evidence_requests",
                "CREATE TABLE underwriting.quote_evidence_request_reviews"));

        Assert.Contains("CREATE TABLE underwriting.quote_evidence_request_reviews", script);
        Assert.Contains("ix_quote_evidence_request_reviews_request_reviewed_at_utc", script);
        Assert.Contains("ix_quote_evidence_request_reviews_quote_reviewed_at_utc", script);
        Assert.Contains("REFERENCES underwriting.quote_evidence_requests", script);

        Assert.DoesNotContain("REFERENCES public.quotes", script);
        Assert.DoesNotContain("REFERENCES public.submissions", script);
        Assert.DoesNotContain("REFERENCES quotes", script);
        Assert.DoesNotContain("REFERENCES submissions", script);
    }

    private static string Section(string value, string startMarker, string endMarker)
    {
        var start = value.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find script marker '{startMarker}'.");

        var end = value.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(end > start, $"Could not find script marker '{endMarker}'.");

        return value[start..end];
    }
}
