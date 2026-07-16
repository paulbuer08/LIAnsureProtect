using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LIAnsureProtect.Modules.Notifications.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationSubjectViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notification_subject_views",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    scope = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    audience = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    subject_reference_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    subject_reference_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    viewed_through_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_subject_views", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_notification_subject_views_recipient_scope_subject",
                schema: "notifications",
                table: "notification_subject_views",
                columns: new[] { "recipient_user_id", "scope", "audience", "subject_reference_type", "subject_reference_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_subject_views",
                schema: "notifications");
        }
    }
}
