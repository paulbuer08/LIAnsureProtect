using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LIAnsureProtect.Modules.Underwriting.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateUnderwritingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "underwriting");

            migrationBuilder.CreateTable(
                name: "ai_underwriting_reviews",
                schema: "underwriting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    quote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_by_user_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    provider_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    prompt_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    output_schema_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    input_snapshot_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    executive_summary = table.Column<string>(type: "text", nullable: true),
                    positive_risk_signals = table.Column<string>(type: "jsonb", nullable: false),
                    negative_risk_signals = table.Column<string>(type: "jsonb", nullable: false),
                    control_gaps = table.Column<string>(type: "jsonb", nullable: false),
                    suggested_underwriting_questions = table.Column<string>(type: "jsonb", nullable: false),
                    suggested_subjectivity_candidates = table.Column<string>(type: "jsonb", nullable: false),
                    citations = table.Column<string>(type: "jsonb", nullable: false),
                    limitations = table.Column<string>(type: "jsonb", nullable: false),
                    advisory_disclaimer = table.Column<string>(type: "text", nullable: true),
                    failure_reason = table.Column<string>(type: "text", nullable: true),
                    feedback = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_underwriting_reviews", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ai_underwriting_reviews_quote_id_created_at_utc",
                schema: "underwriting",
                table: "ai_underwriting_reviews",
                columns: new[] { "quote_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_ai_underwriting_reviews_status_created_at_utc",
                schema: "underwriting",
                table: "ai_underwriting_reviews",
                columns: new[] { "status", "created_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_underwriting_reviews",
                schema: "underwriting");
        }
    }
}
