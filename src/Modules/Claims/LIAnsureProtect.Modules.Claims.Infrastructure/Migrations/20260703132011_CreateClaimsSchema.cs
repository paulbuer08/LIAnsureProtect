using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LIAnsureProtect.Modules.Claims.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateClaimsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "claims");

            migrationBuilder.CreateTable(
                name: "claims",
                schema: "claims",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    claim_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    incident_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    incident_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    discovered_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    policy_number_at_filing = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    policy_effective_at_filing = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    policy_expiration_at_filing = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    policy_limit_at_filing = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    policy_retention_at_filing = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    filed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_claims", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "claims",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    publish_attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_publish_attempt_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    next_attempt_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    provider_message_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    failed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "claim_timeline_entries",
                schema: "claims",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    created_by_user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_claim_timeline_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_claim_timeline_entries_claims_claim_id",
                        column: x => x.claim_id,
                        principalSchema: "claims",
                        principalTable: "claims",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_claim_timeline_entries_claim_id_created_at_utc",
                schema: "claims",
                table: "claim_timeline_entries",
                columns: new[] { "claim_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_claims_owner_user_id",
                schema: "claims",
                table: "claims",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_claims_policy_id",
                schema: "claims",
                table: "claims",
                column: "policy_id");

            migrationBuilder.CreateIndex(
                name: "ix_claims_status_filed_at_utc",
                schema: "claims",
                table: "claims",
                columns: new[] { "status", "filed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_claims_claim_number",
                schema: "claims",
                table: "claims",
                column: "claim_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_dispatch_retry",
                schema: "claims",
                table: "outbox_messages",
                columns: new[] { "processed_at_utc", "failed_at_utc", "next_attempt_at_utc", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_processed_at_utc_created_at_utc",
                schema: "claims",
                table: "outbox_messages",
                columns: new[] { "processed_at_utc", "created_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "claim_timeline_entries",
                schema: "claims");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "claims");

            migrationBuilder.DropTable(
                name: "claims",
                schema: "claims");
        }
    }
}
