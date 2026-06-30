using LIAnsureProtect.Modules.Underwriting.Application;
using LIAnsureProtect.Modules.Underwriting.Application.Ai;
using LIAnsureProtect.Modules.Underwriting.Infrastructure.Ai;
using LIAnsureProtect.Modules.Underwriting.Infrastructure.Persistence;
using LIAnsureProtect.Platform.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LIAnsureProtect.Modules.Underwriting.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the Underwriting module: its own <see cref="UnderwritingDbContext"/> (owning the
    /// <c>underwriting</c> schema), the AI review repository, the advisory AI provider (selected by
    /// deployment profile), and the module's MediatR handlers. The cross-context quote-read adapter
    /// (<c>IUnderwritingQuoteContextReader</c>) is registered on the legacy/Quoting side.
    /// </summary>
    public static IServiceCollection AddUnderwritingModule(
        this IServiceCollection services,
        string? databaseConnectionString,
        PlatformProfile profile = PlatformProfile.Local)
    {
        if (string.IsNullOrWhiteSpace(databaseConnectionString))
            throw new InvalidOperationException("Connection string 'LIAnsureProtect' is required.");

        services.AddDbContext<UnderwritingDbContext>(options =>
        {
            options.UseNpgsql(databaseConnectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", UnderwritingDbContext.SchemaName));
        });

        services.AddScoped<IAiUnderwritingReviewRepository, EfAiUnderwritingReviewRepository>();

        // Ports & Adapters: the advisory AI provider is chosen by the active deployment profile.
        switch (profile)
        {
            case PlatformProfile.Local:
                services.AddScoped<IAiReviewService, LocalSimulatedAiReviewService>();
                break;
            case PlatformProfile.Aws:
                throw new NotSupportedException(
                    "The AWS AI review provider (Bedrock) arrives in a later milestone. " +
                    "Set Platform:Profile=Local until then.");
            default:
                throw new NotSupportedException($"Unsupported Platform:Profile '{profile}'.");
        }

        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(typeof(IAiUnderwritingReviewRepository).Assembly));

        return services;
    }
}
