using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LIAnsureProtect.Modules.Underwriting.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEvidenceRequestIdentityAndDocumentRequirement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "company_name",
                schema: "underwriting",
                table: "quote_evidence_requests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "Company not available for legacy request");

            migrationBuilder.AddColumn<string>(
                name: "document_requirement",
                schema: "underwriting",
                table: "quote_evidence_requests",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Optional");

            migrationBuilder.AddColumn<string>(
                name: "submission_reference",
                schema: "underwriting",
                table: "quote_evidence_requests",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE underwriting.quote_evidence_requests
                SET submission_reference = SUBSTRING('SUB-LEGACY-' || REPLACE(submission_id::text, '-', '') FROM 1 FOR 30);
                """);

            migrationBuilder.AlterColumn<string>(
                name: "submission_reference",
                schema: "underwriting",
                table: "quote_evidence_requests",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "company_name",
                schema: "underwriting",
                table: "quote_evidence_requests");

            migrationBuilder.DropColumn(
                name: "document_requirement",
                schema: "underwriting",
                table: "quote_evidence_requests");

            migrationBuilder.DropColumn(
                name: "submission_reference",
                schema: "underwriting",
                table: "quote_evidence_requests");
        }
    }
}
