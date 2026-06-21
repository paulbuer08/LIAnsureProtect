using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LIAnsureProtect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteAcceptanceAndPolicyBinding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "accepted_at_utc",
                table: "quotes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "accepted_by_name",
                table: "quotes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "accepted_by_title",
                table: "quotes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "accepted_by_user_id",
                table: "quotes",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "subjectivities_acknowledged",
                table: "quotes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "policies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    quote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    policy_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    premium = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    requested_limit = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    retention = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    effective_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expiration_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    bound_by_user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    bound_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    quote_status_at_bind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    quote_risk_tier_at_bind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    quote_subjectivities_at_bind = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_policies", x => x.id);
                    table.ForeignKey(
                        name: "FK_policies_quotes_quote_id",
                        column: x => x.quote_id,
                        principalTable: "quotes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "policy_binding_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    binding_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    failure_reason = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_policy_binding_attempts", x => x.id);
                    table.ForeignKey(
                        name: "FK_policy_binding_attempts_policies_policy_id",
                        column: x => x.policy_id,
                        principalTable: "policies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_policies_owner_user_id_bound_at_utc",
                table: "policies",
                columns: new[] { "owner_user_id", "bound_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_policies_policy_number",
                table: "policies",
                column: "policy_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_policies_quote_id",
                table: "policies",
                column: "quote_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_policy_binding_attempts_policy_id_created_at_utc",
                table: "policy_binding_attempts",
                columns: new[] { "policy_id", "created_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "policy_binding_attempts");

            migrationBuilder.DropTable(
                name: "policies");

            migrationBuilder.DropColumn(
                name: "accepted_at_utc",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "accepted_by_name",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "accepted_by_title",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "accepted_by_user_id",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "subjectivities_acknowledged",
                table: "quotes");
        }
    }
}
