using LIAnsureProtect.Application.Common.Idempotency;
using LIAnsureProtect.Application.Common.Persistence;
using LIAnsureProtect.Application.Policies;
using LIAnsureProtect.Application.Policies.Binding;
using LIAnsureProtect.Application.Quotes;
using LIAnsureProtect.Application.Quotes.RatingProviders;
using LIAnsureProtect.Application.Submissions;
using LIAnsureProtect.Infrastructure.Documents;
using LIAnsureProtect.Infrastructure.Persistence;
using LIAnsureProtect.Infrastructure.Persistence.Idempotency;
using LIAnsureProtect.Infrastructure.Persistence.Outbox.Consumers;
using LIAnsureProtect.Infrastructure.Persistence.Outbox;
using LIAnsureProtect.Infrastructure.Policies;
using LIAnsureProtect.Infrastructure.Quotes;
using LIAnsureProtect.Infrastructure.Quotes.RatingProviders;
using LIAnsureProtect.Infrastructure.Submissions;
using LIAnsureProtect.Modules.Quoting.Application.ReferralDecisions;
using LIAnsureProtect.Modules.Underwriting.Application;
using LIAnsureProtect.Platform.Abstractions;
using LIAnsureProtect.Platform.Abstractions.Documents;
using LIAnsureProtect.Platform.Abstractions.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace LIAnsureProtect.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string? databaseConnectionString,
        PlatformProfile profile = PlatformProfile.Local)
    {
        if (string.IsNullOrWhiteSpace(databaseConnectionString))
            throw new InvalidOperationException("Connection string 'LIAnsureProtect' is required.");

        services.AddDbContext<SubmissionDbContext>(options =>
        {
            options.UseNpgsql(databaseConnectionString);
        });

        services.AddScoped<ISubmissionRepository, EfCoreSubmissionRepository>();
        services.AddScoped<IQuoteRepository, EfCoreQuoteRepository>();
        services.AddScoped<IQuoteReferralDecisionService, QuoteReferralDecisionService>();
        services.AddScoped<IPolicyRepository, EfCorePolicyRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();
        services.AddScoped<IIdempotencyService, EfCoreIdempotencyService>();
        services.AddScoped<IIdempotencyRecordCleanup, EfCoreIdempotencyRecordCleanup>();
        // OutboxDispatcher depends on the Notifications module's INotificationProjector and
        // INotificationPublisher, which AddNotificationsModule(...) registers in the composition root.
        services.AddScoped<IOutboxSource, SubmissionOutboxSource>();
        services.AddScoped<IOutboxMessageConsumer, ReferralOperationOutboxMessageConsumer>();
        services.AddScoped<IOutboxMessageConsumer, NotificationOutboxMessageConsumer>();
        services.AddScoped<IOutboxDispatcher, OutboxDispatcher>();
        services.AddOptions<DocumentStorageOptions>();

        // Ports & Adapters: the document-storage adapter is chosen by the active deployment profile.
        // This is the first concrete proof of the Local <-> AWS switch; more ports follow in later milestones.
        switch (profile)
        {
            case PlatformProfile.Local:
                services.AddScoped<IDocumentStorageService, LocalDocumentStorageService>();
                break;
            case PlatformProfile.Aws:
                throw new NotSupportedException(
                    "The AWS document storage adapter (S3) arrives in Milestone 42. " +
                    "Set Platform:Profile=Local until then.");
            default:
                throw new NotSupportedException($"Unsupported Platform:Profile '{profile}'.");
        }
        services.AddScoped<IPolicyBindingProviderClient, SimulatedPolicyBindingProviderClient>();
        // Quoting-side adapter for the Underwriting module's cross-context quote-read port.
        services.AddScoped<IUnderwritingQuoteContextReader, QuoteUnderwritingContextReader>();
        services.AddTransient<RatingProviderAttemptCountingHandler>();
        services.AddTransient<SimulatedRatingProviderHttpMessageHandler>();
        var ratingProviderClientBuilder = services
            .AddHttpClient<IRatingProviderClient, RatingProviderHttpClient>(client =>
            {
                client.BaseAddress = new Uri("https://local-rating-provider.liansureprotect.test/");
            });
        ratingProviderClientBuilder.AddStandardResilienceHandler(options =>
            {
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(5);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
                options.Retry.MaxRetryAttempts = 2;
                options.Retry.Delay = TimeSpan.FromMilliseconds(25);
                options.CircuitBreaker.FailureRatio = 0.5;
                options.CircuitBreaker.MinimumThroughput = 4;
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(10);
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(5);
            });
        ratingProviderClientBuilder
            .AddHttpMessageHandler<RatingProviderAttemptCountingHandler>()
            .ConfigurePrimaryHttpMessageHandler<SimulatedRatingProviderHttpMessageHandler>();

        return services;
    }
}
