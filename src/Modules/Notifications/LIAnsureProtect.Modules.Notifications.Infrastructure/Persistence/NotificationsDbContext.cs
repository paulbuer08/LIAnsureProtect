using LIAnsureProtect.Modules.Notifications.Domain;
using LIAnsureProtect.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Modules.Notifications.Infrastructure.Persistence;

/// <summary>
/// The Notifications module's own DbContext. It inherits <see cref="ModuleDbContext"/> (schema-per-module
/// + the transactional domain-event capture template) and owns the dedicated <c>notifications</c> schema.
/// </summary>
public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options)
    : ModuleDbContext(options)
{
    public const string SchemaName = "notifications";

    public DbSet<NotificationInboxEntry> NotificationInboxEntries => Set<NotificationInboxEntry>();

    public DbSet<TeamNotificationEntry> TeamNotificationEntries => Set<TeamNotificationEntry>();

    protected override string? Schema => SchemaName;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);
    }
}
