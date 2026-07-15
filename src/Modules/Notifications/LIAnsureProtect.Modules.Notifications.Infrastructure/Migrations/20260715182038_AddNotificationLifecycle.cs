using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LIAnsureProtect.Modules.Notifications.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "historical_at_utc",
                schema: "notifications",
                table: "team_notification_entries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "historical_reason",
                schema: "notifications",
                table: "team_notification_entries",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "lifecycle_state",
                schema: "notifications",
                table: "team_notification_entries",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<Guid>(
                name: "replacement_quote_id",
                schema: "notifications",
                table: "team_notification_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "replacement_quote_version",
                schema: "notifications",
                table: "team_notification_entries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "historical_at_utc",
                schema: "notifications",
                table: "notification_inbox_entries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "historical_reason",
                schema: "notifications",
                table: "notification_inbox_entries",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "lifecycle_state",
                schema: "notifications",
                table: "notification_inbox_entries",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<Guid>(
                name: "replacement_quote_id",
                schema: "notifications",
                table: "notification_inbox_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "replacement_quote_version",
                schema: "notifications",
                table: "notification_inbox_entries",
                type: "integer",
                nullable: true);

            // Reconcile immutable notification snapshots created before quote lifecycle columns existed.
            // The migration reads quote identity/version once and writes only Notifications-owned data;
            // runtime inbox reads remain completely context-local.
            migrationBuilder.Sql(
                """
                UPDATE notifications.notification_inbox_entries AS entry
                SET attributes = jsonb_set(entry.attributes, '{quoteVersion}', to_jsonb(quote.version::text), true)
                FROM public.quotes AS quote
                WHERE entry.attributes ->> 'quoteId' = quote.id::text;

                UPDATE notifications.team_notification_entries AS entry
                SET attributes = jsonb_set(entry.attributes, '{quoteVersion}', to_jsonb(quote.version::text), true)
                FROM public.quotes AS quote
                WHERE entry.attributes ->> 'quoteId' = quote.id::text;

                UPDATE notifications.notification_inbox_entries AS entry
                SET lifecycle_state = 'Historical',
                    historical_at_utc = COALESCE(old_quote.superseded_at_utc, replacement.created_at_utc),
                    historical_reason = 'Superseded by quote version ' || replacement.version || '.',
                    replacement_quote_id = replacement.id,
                    replacement_quote_version = replacement.version
                FROM public.quotes AS old_quote
                JOIN public.quotes AS replacement
                  ON replacement.supersedes_quote_id = old_quote.id
                WHERE entry.attributes ->> 'quoteId' = old_quote.id::text
                  AND old_quote.status = 'Superseded';

                UPDATE notifications.team_notification_entries AS entry
                SET lifecycle_state = 'Historical',
                    historical_at_utc = COALESCE(old_quote.superseded_at_utc, replacement.created_at_utc),
                    historical_reason = 'Superseded by quote version ' || replacement.version || '.',
                    replacement_quote_id = replacement.id,
                    replacement_quote_version = replacement.version
                FROM public.quotes AS old_quote
                JOIN public.quotes AS replacement
                  ON replacement.supersedes_quote_id = old_quote.id
                WHERE entry.attributes ->> 'quoteId' = old_quote.id::text
                  AND old_quote.status = 'Superseded';
                """);

            migrationBuilder.CreateIndex(
                name: "ix_team_notification_entries_audience_lifecycle_created_at_utc",
                schema: "notifications",
                table: "team_notification_entries",
                columns: new[] { "audience", "lifecycle_state", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_inbox_entries_recipient_lifecycle_read",
                schema: "notifications",
                table: "notification_inbox_entries",
                columns: new[] { "recipient_user_id", "lifecycle_state", "read_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_team_notification_entries_audience_lifecycle_created_at_utc",
                schema: "notifications",
                table: "team_notification_entries");

            migrationBuilder.DropIndex(
                name: "ix_notification_inbox_entries_recipient_lifecycle_read",
                schema: "notifications",
                table: "notification_inbox_entries");

            migrationBuilder.DropColumn(
                name: "historical_at_utc",
                schema: "notifications",
                table: "team_notification_entries");

            migrationBuilder.DropColumn(
                name: "historical_reason",
                schema: "notifications",
                table: "team_notification_entries");

            migrationBuilder.DropColumn(
                name: "lifecycle_state",
                schema: "notifications",
                table: "team_notification_entries");

            migrationBuilder.DropColumn(
                name: "replacement_quote_id",
                schema: "notifications",
                table: "team_notification_entries");

            migrationBuilder.DropColumn(
                name: "replacement_quote_version",
                schema: "notifications",
                table: "team_notification_entries");

            migrationBuilder.DropColumn(
                name: "historical_at_utc",
                schema: "notifications",
                table: "notification_inbox_entries");

            migrationBuilder.DropColumn(
                name: "historical_reason",
                schema: "notifications",
                table: "notification_inbox_entries");

            migrationBuilder.DropColumn(
                name: "lifecycle_state",
                schema: "notifications",
                table: "notification_inbox_entries");

            migrationBuilder.DropColumn(
                name: "replacement_quote_id",
                schema: "notifications",
                table: "notification_inbox_entries");

            migrationBuilder.DropColumn(
                name: "replacement_quote_version",
                schema: "notifications",
                table: "notification_inbox_entries");
        }
    }
}
