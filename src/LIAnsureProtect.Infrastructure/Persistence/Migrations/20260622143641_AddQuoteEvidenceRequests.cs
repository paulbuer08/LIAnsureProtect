using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LIAnsureProtect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteEvidenceRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "quote_evidence_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    quote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quote_referral_operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    due_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    requested_by_user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    requested_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    responded_by_user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    respondent_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    respondent_title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    response_text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    attachment_file_name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    attachment_content_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    attachment_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    responded_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    accepted_by_user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    accepted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelled_by_user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    cancelled_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    review_notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quote_evidence_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_quote_evidence_requests_quote_referral_operations_quote_ref~",
                        column: x => x.quote_referral_operation_id,
                        principalTable: "quote_referral_operations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_quote_evidence_requests_quotes_quote_id",
                        column: x => x.quote_id,
                        principalTable: "quotes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_quote_evidence_requests_submissions_submission_id",
                        column: x => x.submission_id,
                        principalTable: "submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_quote_evidence_requests_owner_status_due_at_utc",
                table: "quote_evidence_requests",
                columns: new[] { "owner_user_id", "status", "due_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_quote_evidence_requests_quote_referral_operation_id",
                table: "quote_evidence_requests",
                column: "quote_referral_operation_id");

            migrationBuilder.CreateIndex(
                name: "ix_quote_evidence_requests_quote_status_updated_at_utc",
                table: "quote_evidence_requests",
                columns: new[] { "quote_id", "status", "updated_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_quote_evidence_requests_submission_id",
                table: "quote_evidence_requests",
                column: "submission_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quote_evidence_requests");
        }
    }
}
