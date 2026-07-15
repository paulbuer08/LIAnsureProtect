using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LIAnsureProtect.Modules.Underwriting.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteEvidenceDisposition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "quote_disposition",
                schema: "underwriting",
                table: "quote_evidence_requests",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Current");

            migrationBuilder.AddColumn<int>(
                name: "quote_version",
                schema: "underwriting",
                table: "quote_evidence_requests",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "superseded_at_utc",
                schema: "underwriting",
                table: "quote_evidence_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "superseded_by_quote_id",
                schema: "underwriting",
                table: "quote_evidence_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "superseded_by_quote_version",
                schema: "underwriting",
                table: "quote_evidence_requests",
                type: "integer",
                nullable: true);

            // One-time reconciliation for rows projected before quote_version/disposition existed.
            // Runtime contexts remain isolated; this migration reads the authoritative Quoting snapshot
            // and writes only Underwriting-owned columns.
            migrationBuilder.Sql(
                """
                UPDATE underwriting.quote_evidence_requests AS evidence
                SET quote_version = quote.version
                FROM public.quotes AS quote
                WHERE evidence.quote_id = quote.id;

                UPDATE underwriting.quote_evidence_requests AS evidence
                SET quote_disposition = 'Superseded',
                    superseded_at_utc = COALESCE(old_quote.superseded_at_utc, replacement.created_at_utc),
                    superseded_by_quote_id = replacement.id,
                    superseded_by_quote_version = replacement.version
                FROM public.quotes AS old_quote
                JOIN public.quotes AS replacement
                  ON replacement.supersedes_quote_id = old_quote.id
                WHERE evidence.quote_id = old_quote.id
                  AND old_quote.status = 'Superseded';
                """);

            migrationBuilder.CreateIndex(
                name: "ix_quote_evidence_requests_submission_disposition_version",
                schema: "underwriting",
                table: "quote_evidence_requests",
                columns: new[] { "submission_id", "quote_disposition", "quote_version" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_quote_evidence_requests_submission_disposition_version",
                schema: "underwriting",
                table: "quote_evidence_requests");

            migrationBuilder.DropColumn(
                name: "quote_disposition",
                schema: "underwriting",
                table: "quote_evidence_requests");

            migrationBuilder.DropColumn(
                name: "quote_version",
                schema: "underwriting",
                table: "quote_evidence_requests");

            migrationBuilder.DropColumn(
                name: "superseded_at_utc",
                schema: "underwriting",
                table: "quote_evidence_requests");

            migrationBuilder.DropColumn(
                name: "superseded_by_quote_id",
                schema: "underwriting",
                table: "quote_evidence_requests");

            migrationBuilder.DropColumn(
                name: "superseded_by_quote_version",
                schema: "underwriting",
                table: "quote_evidence_requests");
        }
    }
}
