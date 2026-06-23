using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LIAnsureProtect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteEvidenceDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "quote_evidence_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    evidence_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    content_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    uploaded_by_user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    uploaded_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quote_evidence_documents", x => x.id);
                    table.ForeignKey(
                        name: "FK_quote_evidence_documents_quote_evidence_requests_evidence_r~",
                        column: x => x.evidence_request_id,
                        principalTable: "quote_evidence_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_quote_evidence_documents_quotes_quote_id",
                        column: x => x.quote_id,
                        principalTable: "quotes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_quote_evidence_documents_submissions_submission_id",
                        column: x => x.submission_id,
                        principalTable: "submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_quote_evidence_documents_owner_request",
                table: "quote_evidence_documents",
                columns: new[] { "owner_user_id", "evidence_request_id" });

            migrationBuilder.CreateIndex(
                name: "IX_quote_evidence_documents_quote_id",
                table: "quote_evidence_documents",
                column: "quote_id");

            migrationBuilder.CreateIndex(
                name: "ix_quote_evidence_documents_request_uploaded_at_utc",
                table: "quote_evidence_documents",
                columns: new[] { "evidence_request_id", "uploaded_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_quote_evidence_documents_submission_id",
                table: "quote_evidence_documents",
                column: "submission_id");

            migrationBuilder.CreateIndex(
                name: "ux_quote_evidence_documents_storage_key",
                table: "quote_evidence_documents",
                column: "storage_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quote_evidence_documents");
        }
    }
}
