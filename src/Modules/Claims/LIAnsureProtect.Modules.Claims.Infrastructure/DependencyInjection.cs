using LIAnsureProtect.Modules.Claims.Application;
using LIAnsureProtect.Modules.Claims.Infrastructure.Persistence;
using LIAnsureProtect.Platform.Abstractions;
using LIAnsureProtect.Platform.Abstractions.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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

        // The profile parameter keeps the module's registration signature uniform with the other
        // modules; the Claims module has no profile-switched adapters yet (documents arrive in CM3
        // through the shared Platform storage port).
        _ = profile;

        services.AddDbContext<ClaimsDbContext>(options =>
        {
            options.UseNpgsql(databaseConnectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", ClaimsDbContext.SchemaName));
        });

        services.AddScoped<IClaimRepository, EfClaimRepository>();
        services.AddScoped<IClaimsReader, ClaimsReader>();
        services.AddScoped<IClaimsAdjudicationReader, ClaimsAdjudicationReader>();
        services.AddScoped<IOutboxSource, ClaimsOutboxSource>();

        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(typeof(IClaimRepository).Assembly));

        return services;
    }
}
