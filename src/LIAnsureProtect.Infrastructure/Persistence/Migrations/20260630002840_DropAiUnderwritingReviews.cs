using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LIAnsureProtect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropAiUnderwritingReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_underwriting_reviews");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_underwriting_reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    quote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    advisory_disclaimer = table.Column<string>(type: "text", nullable: true),
                    citations = table.Column<string>(type: "jsonb", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    control_gaps = table.Column<string>(type: "jsonb", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    executive_summary = table.Column<string>(type: "text", nullable: true),
                    failure_reason = table.Column<string>(type: "text", nullable: true),
                    feedback = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    input_snapshot_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    limitations = table.Column<string>(type: "jsonb", nullable: false),
                    negative_risk_signals = table.Column<string>(type: "jsonb", nullable: false),
                    output_schema_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    positive_risk_signals = table.Column<string>(type: "jsonb", nullable: false),
                    prompt_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    provider_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    requested_by_user_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    suggested_subjectivity_candidates = table.Column<string>(type: "jsonb", nullable: false),
                    suggested_underwriting_questions = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_underwriting_reviews", x => x.id);
                    table.ForeignKey(
                        name: "FK_ai_underwriting_reviews_quotes_quote_id",
                        column: x => x.quote_id,
                        principalTable: "quotes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ai_underwriting_reviews_quote_id_created_at_utc",
                table: "ai_underwriting_reviews",
                columns: new[] { "quote_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_ai_underwriting_reviews_status_created_at_utc",
                table: "ai_underwriting_reviews",
                columns: new[] { "status", "created_at_utc" });
        }
    }
}
