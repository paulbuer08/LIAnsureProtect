using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LIAnsureProtect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteSupersessionAndReassessmentGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "superseded_at_utc",
                table: "quotes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE public.quotes AS old_quote
                SET superseded_at_utc = replacement.created_at_utc
                FROM public.quotes AS replacement
                WHERE replacement.supersedes_quote_id = old_quote.id
                  AND old_quote.status = 'Superseded'
                  AND old_quote.superseded_at_utc IS NULL;
                """);

            migrationBuilder.CreateTable(
                name: "reassessment_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_quote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_quote_version = table.Column<int>(type: "integer", nullable: false),
                    owner_user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    request_payload = table.Column<string>(type: "jsonb", nullable: false),
                    request_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    requested_by_user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    requested_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reviewed_by_user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    reviewed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    decision_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_quote_id = table.Column<Guid>(type: "uuid", nullable: true),
                    submission_reference = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    company_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reassessment_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_reassessment_requests_quotes_base_quote_id",
                        column: x => x.base_quote_id,
                        principalTable: "quotes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reassessment_requests_submissions_submission_id",
                        column: x => x.submission_id,
                        principalTable: "submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reassessment_requests_base_quote_id",
                table: "reassessment_requests",
                column: "base_quote_id");

            migrationBuilder.CreateIndex(
                name: "ix_reassessment_requests_owner_submission_requested_at_utc",
                table: "reassessment_requests",
                columns: new[] { "owner_user_id", "submission_id", "requested_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_reassessment_requests_status_requested_at_utc",
                table: "reassessment_requests",
                columns: new[] { "status", "requested_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_reassessment_requests_submission_pending",
                table: "reassessment_requests",
                column: "submission_id",
                unique: true,
                filter: "status = 'Pending'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reassessment_requests");

            migrationBuilder.DropColumn(
                name: "superseded_at_utc",
                table: "quotes");
        }
    }
}
