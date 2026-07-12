using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LIAnsureProtect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteControlAssurance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "assurance_status",
                table: "quotes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "SelfAttested");

            migrationBuilder.AddColumn<string>(
                name: "attestation_wording_version",
                table: "quotes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "attested_at_utc",
                table: "quotes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "attested_by_name",
                table: "quotes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "attested_by_title",
                table: "quotes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "attested_by_user_id",
                table: "quotes",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "evidence_required_count",
                table: "quotes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "evidence_satisfied_count",
                table: "quotes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "supersedes_quote_id",
                table: "quotes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "quotes",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "quote_control_assertions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    quote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quote_version = table.Column<int>(type: "integer", nullable: false),
                    control_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    claimed_state = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    assurance_state = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    evidence_required = table.Column<bool>(type: "boolean", nullable: false),
                    evidence_reason = table.Column<string>(type: "text", nullable: false),
                    captured_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    verified_by_user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    verified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quote_control_assertions", x => x.id);
                    table.ForeignKey(
                        name: "FK_quote_control_assertions_quotes_quote_id",
                        column: x => x.quote_id,
                        principalTable: "quotes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_quotes_submission_id_version",
                table: "quotes",
                columns: new[] { "submission_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_quote_control_assertions_quote_id_control_type",
                table: "quote_control_assertions",
                columns: new[] { "quote_id", "control_type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quote_control_assertions");

            migrationBuilder.DropIndex(
                name: "ux_quotes_submission_id_version",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "assurance_status",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "attestation_wording_version",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "attested_at_utc",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "attested_by_name",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "attested_by_title",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "attested_by_user_id",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "evidence_required_count",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "evidence_satisfied_count",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "supersedes_quote_id",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "version",
                table: "quotes");
        }
    }
}
