using LIAnsureProtect.Application;
using LIAnsureProtect.Application.Common.Idempotency;
using LIAnsureProtect.Application.Common.Persistence;
using LIAnsureProtect.Application.Documents;
using LIAnsureProtect.Application.Notifications;
using LIAnsureProtect.Application.Policies;
using LIAnsureProtect.Application.Policies.Binding;
using LIAnsureProtect.Application.Quotes;
using LIAnsureProtect.Application.Quotes.Ai;
using LIAnsureProtect.Application.Quotes.RatingProviders;
using LIAnsureProtect.Application.Submissions;
using LIAnsureProtect.Infrastructure;
using LIAnsureProtect.Infrastructure.Persistence;
using LIAnsureProtect.Infrastructure.Persistence.Idempotency;
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
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IQuoteRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IPolicyRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IPolicyBindingProviderClient>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IUnitOfWork>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IIdempotencyService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IIdempotencyRecordCleanup>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IOutboxDispatcher>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IRatingProviderClient>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<INotificationPublisher>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAiReviewService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IDocumentStorageService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IEvidenceDocumentScanner>());
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
        Assert.Contains("publish_attempt_count", script);
        Assert.Contains("last_publish_attempt_at_utc", script);
        Assert.Contains("next_attempt_at_utc", script);
        Assert.Contains("provider_message_id", script);
        Assert.Contains("failed_at_utc", script);
        Assert.Contains("CREATE TABLE idempotency_records", script);
        Assert.Contains("ux_idempotency_records_key", script);
        Assert.Contains("ix_idempotency_records_status_completed_at_utc", script);
        Assert.Contains("CREATE TABLE quotes", script);
        Assert.Contains("ix_quotes_submission_id", script);
        Assert.Contains("CREATE TABLE quote_underwriting_reviews", script);
        Assert.Contains("ix_quote_underwriting_reviews_quote_id_created_at_utc", script);
        Assert.Contains("CREATE TABLE quote_rating_provider_attempts", script);
        Assert.Contains("ix_quote_rating_provider_attempts_quote_id_created_at_utc", script);
        Assert.Contains("CREATE TABLE policies", script);
        Assert.Contains("ux_policies_quote_id", script);
        Assert.Contains("ix_policies_owner_user_id_bound_at_utc", script);
        Assert.Contains("CREATE TABLE policy_binding_attempts", script);
        Assert.Contains("ix_policy_binding_attempts_policy_id_created_at_utc", script);
        Assert.Contains("CREATE TABLE ai_underwriting_reviews", script);
        Assert.Contains("ix_ai_underwriting_reviews_quote_id_created_at_utc", script);
        Assert.Contains("ix_ai_underwriting_reviews_status_created_at_utc", script);
        Assert.Contains("CREATE TABLE quote_referral_operations", script);
        Assert.Contains("ux_quote_referral_operations_quote_id", script);
        Assert.Contains("ix_quote_referral_operations_status_priority_due_at_utc", script);
        Assert.Contains("CREATE TABLE quote_referral_work_notes", script);
        Assert.Contains("ix_quote_referral_work_notes_quote_id_created_at_utc", script);
        Assert.Contains("CREATE TABLE quote_referral_follow_up_tasks", script);
        Assert.Contains("ix_quote_referral_follow_up_tasks_quote_id_completed_due_at_utc", script);
        Assert.Contains("CREATE TABLE quote_referral_timeline_entries", script);
        Assert.Contains("ix_quote_referral_timeline_entries_quote_id_created_at_utc", script);
        Assert.Contains("CREATE TABLE quote_evidence_requests", script);
        Assert.Contains("ix_quote_evidence_requests_owner_status_due_at_utc", script);
        Assert.Contains("ix_quote_evidence_requests_quote_status_updated_at_utc", script);
        Assert.Contains("review_decision", script);
        Assert.Contains("review_reason", script);
        Assert.Contains("remediation_guidance", script);
        Assert.Contains("reviewed_by_user_id", script);
        Assert.Contains("reviewed_at_utc", script);
        Assert.Contains("CREATE TABLE quote_evidence_request_reviews", script);
        Assert.Contains("ix_quote_evidence_request_reviews_request_reviewed_at_utc", script);
        Assert.Contains("ix_quote_evidence_request_reviews_quote_reviewed_at_utc", script);
        Assert.Contains("document_count", script);
        Assert.Contains("clean_document_count", script);
        Assert.Contains("CREATE TABLE quote_evidence_documents", script);
        Assert.Contains("ix_quote_evidence_documents_request_uploaded_at_utc", script);
        Assert.Contains("ix_quote_evidence_documents_owner_request", script);
        Assert.Contains("ux_quote_evidence_documents_storage_key", script);
        Assert.Contains("scan_status", script);
        Assert.Contains("scanner_provider_name", script);
        Assert.Contains("scan_result_code", script);
        Assert.Contains("scan_result_reason", script);
        Assert.Contains("scanned_at_utc", script);
        Assert.Contains("sha256", script);
        Assert.Contains("ix_quote_evidence_documents_scan_status_uploaded_at_utc", script);
    }
}
