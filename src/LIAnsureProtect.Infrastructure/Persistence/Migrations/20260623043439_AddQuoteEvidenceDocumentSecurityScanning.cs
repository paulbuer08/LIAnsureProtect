using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LIAnsureProtect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteEvidenceDocumentSecurityScanning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "scan_result_code",
                table: "quote_evidence_documents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "scan_result_reason",
                table: "quote_evidence_documents",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "scan_status",
                table: "quote_evidence_documents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "PendingScan");

            migrationBuilder.AddColumn<DateTime>(
                name: "scanned_at_utc",
                table: "quote_evidence_documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "scanner_provider_name",
                table: "quote_evidence_documents",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sha256",
                table: "quote_evidence_documents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_quote_evidence_documents_scan_status_uploaded_at_utc",
                table: "quote_evidence_documents",
                columns: new[] { "scan_status", "uploaded_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_quote_evidence_documents_scan_status_uploaded_at_utc",
                table: "quote_evidence_documents");

            migrationBuilder.DropColumn(
                name: "scan_result_code",
                table: "quote_evidence_documents");

            migrationBuilder.DropColumn(
                name: "scan_result_reason",
                table: "quote_evidence_documents");

            migrationBuilder.DropColumn(
                name: "scan_status",
                table: "quote_evidence_documents");

            migrationBuilder.DropColumn(
                name: "scanned_at_utc",
                table: "quote_evidence_documents");

            migrationBuilder.DropColumn(
                name: "scanner_provider_name",
                table: "quote_evidence_documents");

            migrationBuilder.DropColumn(
                name: "sha256",
                table: "quote_evidence_documents");
        }
    }
}
