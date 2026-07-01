using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LIAnsureProtect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropEvidenceDocumentRequestForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_quote_evidence_documents_quote_evidence_requests_evidence_r~",
                table: "quote_evidence_documents");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_quote_evidence_documents_quote_evidence_requests_evidence_r~",
                table: "quote_evidence_documents",
                column: "evidence_request_id",
                principalTable: "quote_evidence_requests",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
