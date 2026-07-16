using LIAnsureProtect.Modules.Notifications.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LIAnsureProtect.Modules.Notifications.Infrastructure.Persistence;

public sealed class NotificationSubjectViewConfiguration : IEntityTypeConfiguration<NotificationSubjectView>
{
    public void Configure(EntityTypeBuilder<NotificationSubjectView> builder)
    {
        builder.ToTable("notification_subject_views", NotificationsDbContext.SchemaName);
        builder.HasKey(view => view.Id);
        builder.Property(view => view.Id).HasColumnName("id");
        builder.Property(view => view.RecipientUserId).HasColumnName("recipient_user_id").HasMaxLength(256).IsRequired();
        builder.Property(view => view.Scope).HasColumnName("scope").HasMaxLength(16).IsRequired();
        builder.Property(view => view.Audience).HasColumnName("audience").HasMaxLength(64).IsRequired();
        builder.Property(view => view.SubjectReferenceType).HasColumnName("subject_reference_type").HasMaxLength(64).IsRequired();
        builder.Property(view => view.SubjectReferenceId).HasColumnName("subject_reference_id").HasMaxLength(128).IsRequired();
        builder.Property(view => view.ViewedThroughUtc).HasColumnName("viewed_through_utc").IsRequired();
        builder.Property(view => view.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(view => view.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
        builder.HasIndex(view => new
            {
                view.RecipientUserId,
                view.Scope,
                view.Audience,
                view.SubjectReferenceType,
                view.SubjectReferenceId
            })
            .IsUnique()
            .HasDatabaseName("ux_notification_subject_views_recipient_scope_subject");
    }
}
