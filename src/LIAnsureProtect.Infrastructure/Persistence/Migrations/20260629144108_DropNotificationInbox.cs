using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LIAnsureProtect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropNotificationInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_inbox_entries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notification_inbox_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    attributes = table.Column<string>(type: "jsonb", nullable: false),
                    audience = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    read_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    recipient_user_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    source_outbox_message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_reference_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    subject_reference_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_inbox_entries", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notification_inbox_entries_recipient_read",
                table: "notification_inbox_entries",
                columns: new[] { "recipient_user_id", "read_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_inbox_entries_source_outbox_message_id",
                table: "notification_inbox_entries",
                column: "source_outbox_message_id",
                unique: true);
        }
    }
}
