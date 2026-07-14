using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LIAnsureProtect.Modules.Underwriting.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEvidenceResponseHistoryAndContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "other_concerns",
                schema: "underwriting",
                table: "quote_evidence_requests",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "respondent_email",
                schema: "underwriting",
                table: "quote_evidence_requests",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "respondent_phone",
                schema: "underwriting",
                table: "quote_evidence_requests",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "evidence_response_id",
                schema: "underwriting",
                table: "quote_evidence_documents",
                type: "uuid",
                nullable: true);

            // The earlier compatibility migration used Optional for legacy/manual rows. Requests
            // created by the deterministic assurance policy have always represented material rating
            // assertions and must retain the policy's Required-document contract.
            migrationBuilder.Sql(
                """
                UPDATE underwriting.quote_evidence_requests
                SET document_requirement = 'Required'
                WHERE requested_by_user_id = 'system-assurance-policy'
                  AND document_requirement = 'Optional';
                """);

            migrationBuilder.CreateTable(
                name: "quote_evidence_responses",
                schema: "underwriting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    evidence_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    responded_by_user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    respondent_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    respondent_title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    respondent_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    respondent_phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    response_text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    other_concerns = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    responded_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quote_evidence_responses", x => x.id);
                    table.ForeignKey(
                        name: "FK_quote_evidence_responses_quote_evidence_requests_evidence_r~",
                        column: x => x.evidence_request_id,
                        principalSchema: "underwriting",
                        principalTable: "quote_evidence_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_quote_evidence_documents_response",
                schema: "underwriting",
                table: "quote_evidence_documents",
                column: "evidence_response_id");

            migrationBuilder.CreateIndex(
                name: "ix_quote_evidence_responses_owner_request",
                schema: "underwriting",
                table: "quote_evidence_responses",
                columns: new[] { "owner_user_id", "evidence_request_id" });

            migrationBuilder.CreateIndex(
                name: "ix_quote_evidence_responses_request_responded_at",
                schema: "underwriting",
                table: "quote_evidence_responses",
                columns: new[] { "evidence_request_id", "responded_at_utc", "id" });

            migrationBuilder.AddForeignKey(
                name: "FK_quote_evidence_documents_quote_evidence_responses_evidence_~",
                schema: "underwriting",
                table: "quote_evidence_documents",
                column: "evidence_response_id",
                principalSchema: "underwriting",
                principalTable: "quote_evidence_responses",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_quote_evidence_documents_quote_evidence_responses_evidence_~",
                schema: "underwriting",
                table: "quote_evidence_documents");

            migrationBuilder.DropTable(
                name: "quote_evidence_responses",
                schema: "underwriting");

            migrationBuilder.DropIndex(
                name: "ix_quote_evidence_documents_response",
                schema: "underwriting",
                table: "quote_evidence_documents");

            migrationBuilder.DropColumn(
                name: "other_concerns",
                schema: "underwriting",
                table: "quote_evidence_requests");

            migrationBuilder.DropColumn(
                name: "respondent_email",
                schema: "underwriting",
                table: "quote_evidence_requests");

            migrationBuilder.DropColumn(
                name: "respondent_phone",
                schema: "underwriting",
                table: "quote_evidence_requests");

            migrationBuilder.DropColumn(
                name: "evidence_response_id",
                schema: "underwriting",
                table: "quote_evidence_documents");
        }
    }
}
