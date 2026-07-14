using LIAnsureProtect.Modules.Claims.Application;
using LIAnsureProtect.Modules.Claims.Application.Documents;
using LIAnsureProtect.Modules.Claims.Infrastructure.Documents;
using LIAnsureProtect.Modules.Claims.Infrastructure.Persistence;
using LIAnsureProtect.Platform.Abstractions;
using LIAnsureProtect.Platform.Abstractions.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LIAnsureProtect.Modules.Claims.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the Claims module: its own <see cref="ClaimsDbContext"/> (owning the
    /// <c>claims</c> schema), the claim repository/reader, the module outbox source, and the
    /// module's MediatR handlers. The cross-context policy-read adapter
    /// (<c>IClaimsPolicyContextReader</c>) is registered on the legacy side.
    /// </summary>
    public static IServiceCollection AddClaimsModule(
        this IServiceCollection services,
        string? databaseConnectionString,
        PlatformProfile profile = PlatformProfile.Local)
    {
        if (string.IsNullOrWhiteSpace(databaseConnectionString))
            throw new InvalidOperationException("Connection string 'LIAnsureProtect' is required.");

        services.AddDbContext<ClaimsDbContext>((serviceProvider, options) =>
        {
            var dataSource = serviceProvider.GetService<NpgsqlDataSource>();
            if (dataSource is null)
            {
                options.UseNpgsql(databaseConnectionString, npgsql =>
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", ClaimsDbContext.SchemaName));
            }
            else
            {
                options.UseNpgsql(dataSource, npgsql =>
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", ClaimsDbContext.SchemaName));
            }
        });

        services.AddScoped<IClaimRepository, EfClaimRepository>();
        services.AddScoped<IClaimsReader, ClaimsReader>();
        services.AddScoped<IClaimsAdjudicationReader, ClaimsAdjudicationReader>();
        services.AddScoped<IOutboxSource, ClaimsOutboxSource>();

        // Ports & Adapters: the quarantine scanner is chosen by the active deployment profile
        // (byte storage itself rides the shared Platform IDocumentStorageService registration).
        switch (profile)
        {
            case PlatformProfile.Local:
                services.AddScoped<IClaimDocumentScanner, LocalDeterministicClaimDocumentScanner>();
                break;
            case PlatformProfile.Aws:
                throw new NotSupportedException(
                    "The AWS claim-document scanner (S3-triggered) arrives in a later milestone. " +
                    "Set Platform:Profile=Local until then.");
            default:
                throw new NotSupportedException($"Unsupported Platform:Profile '{profile}'.");
        }

        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(typeof(IClaimRepository).Assembly));

        return services;
    }
}
