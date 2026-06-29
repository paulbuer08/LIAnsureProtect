using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LIAnsureProtect.Modules.Notifications.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamNotificationInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "team_notification_entries",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    audience = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    subject_reference_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    subject_reference_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    attributes = table.Column<string>(type: "jsonb", nullable: false),
                    source_outbox_message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_notification_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "team_notification_read_receipts",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_notification_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_user_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    read_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_notification_read_receipts", x => x.id);
                    table.ForeignKey(
                        name: "FK_team_notification_read_receipts_team_notification_entries_t~",
                        column: x => x.team_notification_entry_id,
                        principalSchema: "notifications",
                        principalTable: "team_notification_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_team_notification_entries_audience_created_at_utc",
                schema: "notifications",
                table: "team_notification_entries",
                columns: new[] { "audience", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_team_notification_entries_source_outbox_message_id",
                schema: "notifications",
                table: "team_notification_entries",
                column: "source_outbox_message_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_team_notification_read_receipts_entry_recipient",
                schema: "notifications",
                table: "team_notification_read_receipts",
                columns: new[] { "team_notification_entry_id", "recipient_user_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "team_notification_read_receipts",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "team_notification_entries",
                schema: "notifications");
        }
    }
}
