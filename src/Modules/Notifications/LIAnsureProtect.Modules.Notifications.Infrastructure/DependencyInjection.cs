using LIAnsureProtect.Modules.Notifications.Application;
using LIAnsureProtect.Modules.Notifications.Infrastructure.Persistence;
using LIAnsureProtect.Platform.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LIAnsureProtect.Modules.Notifications.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the Notifications module: its own <see cref="NotificationsDbContext"/> (owning the
    /// <c>notifications</c> schema), the inbox repository, the inbound projector port, the outbound
    /// publisher (selected by deployment profile), and the module's MediatR handlers.
    /// </summary>
    public static IServiceCollection AddNotificationsModule(
        this IServiceCollection services,
        string? databaseConnectionString,
        PlatformProfile profile = PlatformProfile.Local)
    {
        if (string.IsNullOrWhiteSpace(databaseConnectionString))
            throw new InvalidOperationException("Connection string 'LIAnsureProtect' is required.");

        services.AddDbContext<NotificationsDbContext>(options =>
        {
            options.UseNpgsql(databaseConnectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", NotificationsDbContext.SchemaName));
        });

        services.AddScoped<INotificationInboxRepository, EfNotificationInboxRepository>();
        services.AddScoped<ITeamNotificationRepository, EfTeamNotificationRepository>();
        services.AddScoped<INotificationProjector, NotificationInboxProjector>();

        // Ports & Adapters: the notification publisher is chosen by the active deployment profile.
        switch (profile)
        {
            case PlatformProfile.Local:
                services.AddScoped<INotificationPublisher, LocalNotificationPublisher>();
                break;
            case PlatformProfile.Aws:
                throw new NotSupportedException(
                    "The AWS notification publisher (SNS/SES) arrives in a later milestone. " +
                    "Set Platform:Profile=Local until then.");
            default:
                throw new NotSupportedException($"Unsupported Platform:Profile '{profile}'.");
        }

        // Register this module's MediatR handlers (list + mark-read) from the module Application assembly.
        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(typeof(INotificationProjector).Assembly));

        return services;
    }
}
