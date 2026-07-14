using LIAnsureProtect.Application.Common.Idempotency;
using LIAnsureProtect.Application.Common.Persistence;
using LIAnsureProtect.Application.Policies;
using LIAnsureProtect.Application.Policies.Binding;
using LIAnsureProtect.Application.Quotes;
using LIAnsureProtect.Application.Quotes.RatingProviders;
using LIAnsureProtect.Application.Quotes.Assurance;
using LIAnsureProtect.Application.Submissions;
using LIAnsureProtect.Infrastructure.Documents;
using LIAnsureProtect.Infrastructure.Persistence;
using LIAnsureProtect.Infrastructure.Persistence.Idempotency;
using LIAnsureProtect.Infrastructure.Persistence.Outbox.Consumers;
using LIAnsureProtect.Infrastructure.Persistence.Outbox.Mapping;
using LIAnsureProtect.Infrastructure.Persistence.Outbox.Mapping.Notifications;
using LIAnsureProtect.Infrastructure.Persistence.Outbox.Mapping.ReferralOperations;
using LIAnsureProtect.Infrastructure.Persistence.Outbox.Mapping.Assurance;
using LIAnsureProtect.Infrastructure.Persistence.Outbox;
using LIAnsureProtect.Infrastructure.Policies;
using LIAnsureProtect.Infrastructure.Quotes;
using LIAnsureProtect.Infrastructure.Quotes.RatingProviders;
using LIAnsureProtect.Infrastructure.Submissions;
using LIAnsureProtect.Modules.Claims.Application;
using LIAnsureProtect.Modules.Notifications.Application;
using LIAnsureProtect.Modules.Quoting.Application.ReferralDecisions;
using LIAnsureProtect.Modules.Underwriting.Application;
using LIAnsureProtect.Modules.Underwriting.Application.Referrals;
using LIAnsureProtect.Modules.Underwriting.Application.Assurance;
using LIAnsureProtect.Platform.Abstractions;
using LIAnsureProtect.Platform.Abstractions.Documents;
using LIAnsureProtect.Platform.Abstractions.Outbox;
using LIAnsureProtect.Platform.Abstractions.Caching;
using LIAnsureProtect.Infrastructure.Caching;
using Amazon;
using Amazon.S3;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Npgsql;

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

        services.AddDbContext<SubmissionDbContext>((serviceProvider, options) =>
        {
            var dataSource = serviceProvider.GetService<NpgsqlDataSource>();
            if (dataSource is null)
                options.UseNpgsql(databaseConnectionString);
            else
                options.UseNpgsql(dataSource);
        });

        services.AddScoped<ISubmissionRepository, EfCoreSubmissionRepository>();
        // Keeps the legacy Infrastructure registration independently resolvable in tests/tools.
        // A host's AddNotificationRealtime registration runs first and wins this TryAdd fallback.
        services.TryAddSingleton<INotificationRealtimePublisher, NoOpNotificationRealtimePublisher>();
        services.AddScoped<IQuoteRepository, EfCoreQuoteRepository>();
        services.AddScoped<IQuoteReferralDecisionService, QuoteReferralDecisionService>();
        services.AddScoped<IPolicyRepository, EfCorePolicyRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();
        services.AddScoped<IIdempotencyService, EfCoreIdempotencyService>();
        services.AddScoped<IIdempotencyRecordCleanup, EfCoreIdempotencyRecordCleanup>();
        // OutboxDispatcher depends on the Notifications module's INotificationProjector and
        // INotificationPublisher, which AddNotificationsModule(...) registers in the composition root.
        services.AddScoped<IOutboxSource, SubmissionOutboxSource>();
        services.AddScoped(typeof(OutboxMessageMapperRegistry<>));
        services.AddScoped<IOutboxMessageMapper<NotificationMessage>, QuoteGeneratedNotificationMapper>();
        services.AddScoped<IOutboxMessageMapper<NotificationMessage>, QuoteUnderwritingDecisionRecordedNotificationMapper>();
        services.AddScoped<IOutboxMessageMapper<NotificationMessage>, QuoteAcceptedNotificationMapper>();
        services.AddScoped<IOutboxMessageMapper<NotificationMessage>, PolicyBoundNotificationMapper>();
        services.AddScoped<IOutboxMessageMapper<NotificationMessage>, EvidenceRequestCreatedNotificationMapper>();
        services.AddScoped<IOutboxMessageMapper<NotificationMessage>, EvidenceRequestRespondedNotificationMapper>();
        services.AddScoped<IOutboxMessageMapper<NotificationMessage>, EvidenceRequestAcceptedNotificationMapper>();
        services.AddScoped<IOutboxMessageMapper<NotificationMessage>, EvidenceRequestCancelledNotificationMapper>();
        services.AddScoped<IOutboxMessageMapper<NotificationMessage>, EvidenceRequestFollowUpSentNotificationMapper>();
        services.AddScoped<IOutboxMessageMapper<NotificationMessage>, EvidenceRequestRemediationRequiredNotificationMapper>();
        services.AddScoped<IOutboxMessageMapper<NotificationMessage>, ClaimFiledNotificationMapper>();
        services.AddScoped<IOutboxMessageMapper<NotificationMessage>, ClaimAssignedNotificationMapper>();
        services.AddScoped<IOutboxMessageMapper<NotificationMessage>, ClaimInformationRequestedNotificationMapper>();
        services.AddScoped<IOutboxMessageMapper<NotificationMessage>, ClaimantInformationResponseNotificationMapper>();
        services.AddScoped<IOutboxMessageMapper<NotificationMessage>, ClaimAcceptedNotificationMapper>();
        services.AddScoped<IOutboxMessageMapper<NotificationMessage>, ClaimDeniedNotificationMapper>();
        services.AddScoped<IOutboxMessageMapper<NotificationMessage>, ClaimClosedNotificationMapper>();
        services.AddScoped<IOutboxMessageMapper<ReferralOperationEvent>, QuoteGeneratedReferralOperationMapper>();
        services.AddScoped<IOutboxMessageMapper<ReferralOperationEvent>, QuoteUnderwritingDecisionReferralOperationMapper>();
        services.AddScoped<IOutboxMessageMapper<ReferralOperationEvent>, EvidenceRequestCreatedReferralOperationMapper>();
        services.AddScoped<IOutboxMessageMapper<ReferralOperationEvent>, EvidenceRequestRespondedReferralOperationMapper>();
        services.AddScoped<IOutboxMessageMapper<ReferralOperationEvent>, EvidenceRequestAcceptedReferralOperationMapper>();
        services.AddScoped<IOutboxMessageMapper<ReferralOperationEvent>, EvidenceRequestCancelledReferralOperationMapper>();
        services.AddScoped<IOutboxMessageMapper<ReferralOperationEvent>, EvidenceRequestFollowUpSentReferralOperationMapper>();
        services.AddScoped<IOutboxMessageMapper<ReferralOperationEvent>, EvidenceRequestRemediationRequiredReferralOperationMapper>();
        services.AddScoped<IOutboxMessageConsumer, ReferralOperationOutboxMessageConsumer>();
        services.AddScoped<IOutboxMessageMapper<QuoteAssuranceEvent>, QuoteGeneratedAssuranceMapper>();
        services.AddScoped<IOutboxMessageConsumer, QuoteAssuranceOutboxMessageConsumer>();
        services.AddScoped<IOutboxMessageMapper<QuoteAssuranceDecisionEvent>, EvidenceAcceptedAssuranceDecisionMapper>();
        services.AddScoped<IOutboxMessageMapper<QuoteAssuranceDecisionEvent>, EvidenceRemediationAssuranceDecisionMapper>();
        services.AddScoped<IQuoteAssuranceDecisionProjector, QuoteAssuranceDecisionProjector>();
        services.AddScoped<IOutboxMessageConsumer, QuoteAssuranceDecisionOutboxMessageConsumer>();
        services.AddScoped<IOutboxMessageConsumer, NotificationOutboxMessageConsumer>();
        services.AddScoped<IOutboxDispatcher, OutboxDispatcher>();
        services.AddOptions<DocumentStorageOptions>();

        // Ports & Adapters: the cache adapter is chosen by the active deployment profile.
        // Local uses in-process memory; Aws uses Redis (ElastiCache in real deployments).
        services.AddOptions<CacheOptions>();
        switch (profile)
        {
            case PlatformProfile.Local:
                services.AddMemoryCache();
                services.AddSingleton<ICacheService, InMemoryCacheService>();
                break;
            case PlatformProfile.Aws:
                services.AddStackExchangeRedisCache(_ => { });
                // Fail fast on a missing connection string when the Redis cache is first materialized.
                services.AddOptions<RedisCacheOptions>().Configure<IOptions<CacheOptions>>((redis, cacheOptions) =>
                {
                    redis.Configuration = string.IsNullOrWhiteSpace(cacheOptions.Value.RedisConnectionString)
                        ? throw new InvalidOperationException(
                            "Cache:RedisConnectionString is required when Platform:Profile=Aws.")
                        : cacheOptions.Value.RedisConnectionString;
                });
                services.AddSingleton<ICacheService, RedisCacheService>();
                break;
            default:
                throw new NotSupportedException($"Unsupported Platform:Profile '{profile}'.");
        }

        // Ports & Adapters: the document-storage adapter is chosen by the active deployment profile.
        // This is the first concrete proof of the Local <-> AWS switch; more ports follow in later milestones.
        switch (profile)
        {
            case PlatformProfile.Local:
                services.AddScoped<IDocumentStorageService, LocalDocumentStorageService>();
                break;
            case PlatformProfile.Aws:
                // The same S3 adapter targets real AWS or a local S3-compatible service (LocalStack),
                // decided purely by DocumentStorage:S3 configuration. Fail fast on a missing bucket.
                services.AddSingleton<IAmazonS3>(serviceProvider =>
                {
                    var s3Options = serviceProvider.GetRequiredService<IOptions<DocumentStorageOptions>>().Value.S3
                        ?? throw new InvalidOperationException(
                            "DocumentStorage:S3 configuration is required when Platform:Profile=Aws.");
                    if (string.IsNullOrWhiteSpace(s3Options.BucketName))
                        throw new InvalidOperationException(
                            "DocumentStorage:S3:BucketName is required when Platform:Profile=Aws.");

                    var s3Config = new AmazonS3Config();
                    if (!string.IsNullOrWhiteSpace(s3Options.ServiceUrl))
                    {
                        // LocalStack / S3-compatible endpoint.
                        s3Config.ServiceURL = s3Options.ServiceUrl;
                        s3Config.ForcePathStyle = s3Options.ForcePathStyle;
                    }
                    else if (!string.IsNullOrWhiteSpace(s3Options.Region))
                    {
                        s3Config.RegionEndpoint = RegionEndpoint.GetBySystemName(s3Options.Region);
                    }

                    // Static creds only for LocalStack; empty in real AWS so the default credential
                    // chain (task/instance role) is used — no static keys in the cloud.
                    return string.IsNullOrWhiteSpace(s3Options.AccessKeyId)
                        ? new AmazonS3Client(s3Config)
                        : new AmazonS3Client(s3Options.AccessKeyId, s3Options.SecretAccessKey, s3Config);
                });
                services.AddScoped<IDocumentStorageService, S3DocumentStorageService>();
                break;
            default:
                throw new NotSupportedException($"Unsupported Platform:Profile '{profile}'.");
        }
        services.AddScoped<IPolicyBindingProviderClient, SimulatedPolicyBindingProviderClient>();
        // Quoting-side adapter for the Underwriting module's cross-context quote-read port.
        services.AddScoped<IUnderwritingQuoteContextReader, QuoteUnderwritingContextReader>();
        // Legacy Policy-side adapter for the Claims module's cross-context policy-read port.
        services.AddScoped<IClaimsPolicyContextReader, ClaimsPolicyContextReader>();
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
