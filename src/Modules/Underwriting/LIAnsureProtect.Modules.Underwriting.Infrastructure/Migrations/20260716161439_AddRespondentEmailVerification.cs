using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LIAnsureProtect.Modules.Underwriting.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRespondentEmailVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "email_domain_status",
                schema: "underwriting",
                table: "quote_evidence_responses",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "email_verification_expires_at_utc",
                schema: "underwriting",
                table: "quote_evidence_responses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "email_verification_send_count",
                schema: "underwriting",
                table: "quote_evidence_responses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "email_verification_sent_at_utc",
                schema: "underwriting",
                table: "quote_evidence_responses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "email_verification_status",
                schema: "underwriting",
                table: "quote_evidence_responses",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "email_verification_token_hash",
                schema: "underwriting",
                table: "quote_evidence_responses",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "email_verified_at_utc",
                schema: "underwriting",
                table: "quote_evidence_responses",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "email_domain_status",
                schema: "underwriting",
                table: "quote_evidence_responses");

            migrationBuilder.DropColumn(
                name: "email_verification_expires_at_utc",
                schema: "underwriting",
                table: "quote_evidence_responses");

            migrationBuilder.DropColumn(
                name: "email_verification_send_count",
                schema: "underwriting",
                table: "quote_evidence_responses");

            migrationBuilder.DropColumn(
                name: "email_verification_sent_at_utc",
                schema: "underwriting",
                table: "quote_evidence_responses");

            migrationBuilder.DropColumn(
                name: "email_verification_status",
                schema: "underwriting",
                table: "quote_evidence_responses");

            migrationBuilder.DropColumn(
                name: "email_verification_token_hash",
                schema: "underwriting",
                table: "quote_evidence_responses");

            migrationBuilder.DropColumn(
                name: "email_verified_at_utc",
                schema: "underwriting",
                table: "quote_evidence_responses");
        }
    }
}
