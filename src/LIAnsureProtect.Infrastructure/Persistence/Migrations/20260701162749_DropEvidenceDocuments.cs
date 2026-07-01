using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LIAnsureProtect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropEvidenceDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quote_evidence_documents");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "quote_evidence_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    evidence_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    owner_user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    quote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scan_result_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    scan_result_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    scan_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "PendingScan"),
                    scanned_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    scanner_provider_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    uploaded_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    uploaded_by_user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quote_evidence_documents", x => x.id);
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
                name: "ix_quote_evidence_documents_scan_status_uploaded_at_utc",
                table: "quote_evidence_documents",
                columns: new[] { "scan_status", "uploaded_at_utc" });

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
    }
}
