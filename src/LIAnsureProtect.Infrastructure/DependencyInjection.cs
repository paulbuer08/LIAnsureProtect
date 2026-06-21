using LIAnsureProtect.Application.Common.Idempotency;
using LIAnsureProtect.Application.Common.Persistence;
using LIAnsureProtect.Application.Quotes;
using LIAnsureProtect.Application.Quotes.RatingProviders;
using LIAnsureProtect.Application.Submissions;
using LIAnsureProtect.Infrastructure.Persistence;
using LIAnsureProtect.Infrastructure.Persistence.Idempotency;
using LIAnsureProtect.Infrastructure.Persistence.Outbox;
using LIAnsureProtect.Infrastructure.Quotes;
using LIAnsureProtect.Infrastructure.Quotes.RatingProviders;
using LIAnsureProtect.Infrastructure.Submissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

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
        services.AddScoped<IQuoteRepository, EfCoreQuoteRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();
        services.AddScoped<IIdempotencyService, EfCoreIdempotencyService>();
        services.AddScoped<IIdempotencyRecordCleanup, EfCoreIdempotencyRecordCleanup>();
        services.AddScoped<IOutboxDispatcher, OutboxDispatcher>();
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
