using Amazon;
using Amazon.SimpleNotificationService;
using LIAnsureProtect.Modules.Notifications.Application;
using LIAnsureProtect.Modules.Notifications.Infrastructure.Persistence;
using LIAnsureProtect.Modules.Notifications.Infrastructure.Realtime;
using LIAnsureProtect.Platform.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using Npgsql;

namespace LIAnsureProtect.Modules.Notifications.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationRealtime(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(NotificationRealtimeOptions.SectionName);
        var realtimeOptions = section.Get<NotificationRealtimeOptions>() ?? new NotificationRealtimeOptions();

        services.AddOptions<NotificationRealtimeOptions>()
            .Bind(section)
            .Validate(
                options => !options.Enabled || !string.IsNullOrWhiteSpace(options.RedisConnectionString),
                "Notifications:Realtime:RedisConnectionString is required when realtime notifications are enabled.")
            .Validate(
                options => !options.Enabled || !string.IsNullOrWhiteSpace(options.ChannelPrefix),
                "Notifications:Realtime:ChannelPrefix is required when realtime notifications are enabled.")
            .ValidateOnStart();

        var signalR = services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = false;
            options.MaximumReceiveMessageSize = 16 * 1024;
        });

        if (realtimeOptions.Enabled)
        {
            signalR.AddStackExchangeRedis(realtimeOptions.RedisConnectionString, options =>
            {
                options.Configuration.ChannelPrefix = RedisChannel.Literal(realtimeOptions.ChannelPrefix);
            });
            services.AddSingleton<INotificationRealtimePublisher, SignalRNotificationRealtimePublisher>();
        }
        else
        {
            services.AddSingleton<INotificationRealtimePublisher, NoOpNotificationRealtimePublisher>();
        }

        services.AddSingleton<IUserIdProvider, NotificationUserIdProvider>();
        return services;
    }

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

        services.AddDbContext<NotificationsDbContext>((serviceProvider, options) =>
        {
            var dataSource = serviceProvider.GetService<NpgsqlDataSource>();
            if (dataSource is null)
            {
                options.UseNpgsql(databaseConnectionString, npgsql =>
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", NotificationsDbContext.SchemaName));
            }
            else
            {
                options.UseNpgsql(dataSource, npgsql =>
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", NotificationsDbContext.SchemaName));
            }
        });

        services.AddScoped<INotificationInboxRepository, EfNotificationInboxRepository>();
        services.AddScoped<ITeamNotificationRepository, EfTeamNotificationRepository>();
        services.AddScoped<INotificationProjector, NotificationInboxProjector>();

        // Ports & Adapters: the notification publisher is chosen by the active deployment profile.
        services.AddOptions<NotificationPublisherOptions>();
        switch (profile)
        {
            case PlatformProfile.Local:
                services.AddScoped<INotificationPublisher, LocalNotificationPublisher>();
                break;
            case PlatformProfile.Aws:
                // The same SNS adapter targets real AWS or a local SNS-compatible service (LocalStack),
                // decided purely by Notifications:Sns configuration. Fail fast on a missing topic.
                services.AddSingleton<IAmazonSimpleNotificationService>(serviceProvider =>
                {
                    var snsOptions = serviceProvider
                        .GetRequiredService<IOptions<NotificationPublisherOptions>>().Value.Sns
                        ?? throw new InvalidOperationException(
                            "Notifications:Sns configuration is required when Platform:Profile=Aws.");
                    if (string.IsNullOrWhiteSpace(snsOptions.TopicArn))
                        throw new InvalidOperationException(
                            "Notifications:Sns:TopicArn is required when Platform:Profile=Aws.");

                    var snsConfig = new AmazonSimpleNotificationServiceConfig();
                    if (!string.IsNullOrWhiteSpace(snsOptions.ServiceUrl))
                    {
                        // LocalStack / SNS-compatible endpoint.
                        snsConfig.ServiceURL = snsOptions.ServiceUrl;
                    }
                    else if (!string.IsNullOrWhiteSpace(snsOptions.Region))
                    {
                        snsConfig.RegionEndpoint = RegionEndpoint.GetBySystemName(snsOptions.Region);
                    }

                    // Static creds only for LocalStack; empty in real AWS so the default credential
                    // chain (task/instance role) is used — no static keys in the cloud.
                    return string.IsNullOrWhiteSpace(snsOptions.AccessKeyId)
                        ? new AmazonSimpleNotificationServiceClient(snsConfig)
                        : new AmazonSimpleNotificationServiceClient(
                            snsOptions.AccessKeyId, snsOptions.SecretAccessKey, snsConfig);
                });
                services.AddScoped<INotificationPublisher, SnsNotificationPublisher>();
                break;
            default:
                throw new NotSupportedException($"Unsupported Platform:Profile '{profile}'.");
        }

        // Register this module's MediatR handlers (list + mark-read) from the module Application assembly.
        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(typeof(INotificationProjector).Assembly));

        return services;
    }
}
