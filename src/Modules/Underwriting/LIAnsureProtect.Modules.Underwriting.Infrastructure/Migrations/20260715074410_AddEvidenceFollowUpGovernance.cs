using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LIAnsureProtect.Modules.Underwriting.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEvidenceFollowUpGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "respondent_mobile_number",
                schema: "underwriting",
                table: "quote_evidence_responses",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "respondent_telephone_number",
                schema: "underwriting",
                table: "quote_evidence_responses",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "viewed_at_utc",
                schema: "underwriting",
                table: "quote_evidence_responses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "viewed_by_user_id",
                schema: "underwriting",
                table: "quote_evidence_responses",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "respondent_mobile_number",
                schema: "underwriting",
                table: "quote_evidence_requests",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "respondent_telephone_number",
                schema: "underwriting",
                table: "quote_evidence_requests",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "version",
                schema: "underwriting",
                table: "quote_evidence_requests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE underwriting.quote_evidence_requests
                SET respondent_mobile_number = CASE
                        WHEN regexp_replace(respondent_phone, '[\\s()\\-]', '', 'g') ~ '^(09[0-9]{9}|\\+?639[0-9]{9})$'
                        THEN CASE
                            WHEN regexp_replace(respondent_phone, '[\\s()\\-]', '', 'g') LIKE '09%'
                            THEN '+63' || substring(regexp_replace(respondent_phone, '[\\s()\\-]', '', 'g') from 2)
                            ELSE '+' || ltrim(regexp_replace(respondent_phone, '[\\s()\\-]', '', 'g'), '+')
                        END
                        ELSE NULL
                    END,
                    respondent_telephone_number = CASE
                        WHEN regexp_replace(respondent_phone, '[\\s()\\-]', '', 'g') ~ '^(0[2-8][0-9]{7,8}|\\+?63[2-8][0-9]{7,8})$'
                        THEN CASE
                            WHEN regexp_replace(respondent_phone, '[\\s()\\-]', '', 'g') LIKE '0%'
                            THEN '+63' || substring(regexp_replace(respondent_phone, '[\\s()\\-]', '', 'g') from 2)
                            ELSE '+' || ltrim(regexp_replace(respondent_phone, '[\\s()\\-]', '', 'g'), '+')
                        END
                        ELSE NULL
                    END
                WHERE respondent_phone IS NOT NULL;

                UPDATE underwriting.quote_evidence_responses
                SET respondent_mobile_number = CASE
                        WHEN regexp_replace(respondent_phone, '[\\s()\\-]', '', 'g') ~ '^(09[0-9]{9}|\\+?639[0-9]{9})$'
                        THEN CASE
                            WHEN regexp_replace(respondent_phone, '[\\s()\\-]', '', 'g') LIKE '09%'
                            THEN '+63' || substring(regexp_replace(respondent_phone, '[\\s()\\-]', '', 'g') from 2)
                            ELSE '+' || ltrim(regexp_replace(respondent_phone, '[\\s()\\-]', '', 'g'), '+')
                        END
                        ELSE NULL
                    END,
                    respondent_telephone_number = CASE
                        WHEN regexp_replace(respondent_phone, '[\\s()\\-]', '', 'g') ~ '^(0[2-8][0-9]{7,8}|\\+?63[2-8][0-9]{7,8})$'
                        THEN CASE
                            WHEN regexp_replace(respondent_phone, '[\\s()\\-]', '', 'g') LIKE '0%'
                            THEN '+63' || substring(regexp_replace(respondent_phone, '[\\s()\\-]', '', 'g') from 2)
                            ELSE '+' || ltrim(regexp_replace(respondent_phone, '[\\s()\\-]', '', 'g'), '+')
                        END
                        ELSE NULL
                    END
                WHERE respondent_phone IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_quote_evidence_responses_request_kind_viewed_at",
                schema: "underwriting",
                table: "quote_evidence_responses",
                columns: new[] { "evidence_request_id", "kind", "viewed_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_quote_evidence_responses_request_kind_viewed_at",
                schema: "underwriting",
                table: "quote_evidence_responses");

            migrationBuilder.DropColumn(
                name: "respondent_mobile_number",
                schema: "underwriting",
                table: "quote_evidence_responses");

            migrationBuilder.DropColumn(
                name: "respondent_telephone_number",
                schema: "underwriting",
                table: "quote_evidence_responses");

            migrationBuilder.DropColumn(
                name: "viewed_at_utc",
                schema: "underwriting",
                table: "quote_evidence_responses");

            migrationBuilder.DropColumn(
                name: "viewed_by_user_id",
                schema: "underwriting",
                table: "quote_evidence_responses");

            migrationBuilder.DropColumn(
                name: "respondent_mobile_number",
                schema: "underwriting",
                table: "quote_evidence_requests");

            migrationBuilder.DropColumn(
                name: "respondent_telephone_number",
                schema: "underwriting",
                table: "quote_evidence_requests");

            migrationBuilder.DropColumn(
                name: "version",
                schema: "underwriting",
                table: "quote_evidence_requests");
        }
    }
}
