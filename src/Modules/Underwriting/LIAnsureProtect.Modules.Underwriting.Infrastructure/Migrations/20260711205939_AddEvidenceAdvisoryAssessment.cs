using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LIAnsureProtect.Modules.Underwriting.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEvidenceAdvisoryAssessment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "advisory_findings_json",
                schema: "underwriting",
                table: "quote_evidence_documents",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "assessment_version",
                schema: "underwriting",
                table: "quote_evidence_documents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "claim_consistency_status",
                schema: "underwriting",
                table: "quote_evidence_documents",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "plausibility_status",
                schema: "underwriting",
                table: "quote_evidence_documents",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "advisory_findings_json",
                schema: "underwriting",
                table: "quote_evidence_documents");

            migrationBuilder.DropColumn(
                name: "assessment_version",
                schema: "underwriting",
                table: "quote_evidence_documents");

            migrationBuilder.DropColumn(
                name: "claim_consistency_status",
                schema: "underwriting",
                table: "quote_evidence_documents");

            migrationBuilder.DropColumn(
                name: "plausibility_status",
                schema: "underwriting",
                table: "quote_evidence_documents");
        }
    }
}
