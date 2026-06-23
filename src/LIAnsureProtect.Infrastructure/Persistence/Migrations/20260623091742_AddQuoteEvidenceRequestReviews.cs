using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LIAnsureProtect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteEvidenceRequestReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "remediation_guidance",
                table: "quote_evidence_requests",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "review_decision",
                table: "quote_evidence_requests",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "NotReviewed");

            migrationBuilder.AddColumn<string>(
                name: "review_reason",
                table: "quote_evidence_requests",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "reviewed_at_utc",
                table: "quote_evidence_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reviewed_by_user_id",
                table: "quote_evidence_requests",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "quote_evidence_request_reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    evidence_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    decision = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    remediation_guidance = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    reviewed_by_user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    reviewed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    document_count = table.Column<int>(type: "integer", nullable: false),
                    clean_document_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quote_evidence_request_reviews", x => x.id);
                    table.ForeignKey(
                        name: "FK_quote_evidence_request_reviews_quote_evidence_requests_evid~",
                        column: x => x.evidence_request_id,
                        principalTable: "quote_evidence_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_quote_evidence_request_reviews_quotes_quote_id",
                        column: x => x.quote_id,
                        principalTable: "quotes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_quote_evidence_request_reviews_submissions_submission_id",
                        column: x => x.submission_id,
                        principalTable: "submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_quote_evidence_request_reviews_quote_reviewed_at_utc",
                table: "quote_evidence_request_reviews",
                columns: new[] { "quote_id", "reviewed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_quote_evidence_request_reviews_request_reviewed_at_utc",
                table: "quote_evidence_request_reviews",
                columns: new[] { "evidence_request_id", "reviewed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_quote_evidence_request_reviews_submission_id",
                table: "quote_evidence_request_reviews",
                column: "submission_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quote_evidence_request_reviews");

            migrationBuilder.DropColumn(
                name: "remediation_guidance",
                table: "quote_evidence_requests");

            migrationBuilder.DropColumn(
                name: "review_decision",
                table: "quote_evidence_requests");

            migrationBuilder.DropColumn(
                name: "review_reason",
                table: "quote_evidence_requests");

            migrationBuilder.DropColumn(
                name: "reviewed_at_utc",
                table: "quote_evidence_requests");

            migrationBuilder.DropColumn(
                name: "reviewed_by_user_id",
                table: "quote_evidence_requests");
        }
    }
}
