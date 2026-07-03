using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LIAnsureProtect.Modules.Claims.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimDecisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "closed_at_utc",
                schema: "claims",
                table: "claims",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "decided_at_utc",
                schema: "claims",
                table: "claims",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "decided_by_user_id",
                schema: "claims",
                table: "claims",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "denial_narrative",
                schema: "claims",
                table: "claims",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "denial_reason",
                schema: "claims",
                table: "claims",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "settlement_amount",
                schema: "claims",
                table: "claims",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "claim_decisions",
                schema: "claims",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_id = table.Column<Guid>(type: "uuid", nullable: false),
                    outcome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    settlement_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    denial_reason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    claimed_amount_at_decision = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    reserve_amount_at_decision = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    decided_by_user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    decided_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_claim_decisions", x => x.id);
                    table.ForeignKey(
                        name: "FK_claim_decisions_claims_claim_id",
                        column: x => x.claim_id,
                        principalSchema: "claims",
                        principalTable: "claims",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_claim_decisions_claim_id_decided_at_utc",
                schema: "claims",
                table: "claim_decisions",
                columns: new[] { "claim_id", "decided_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "claim_decisions",
                schema: "claims");

            migrationBuilder.DropColumn(
                name: "closed_at_utc",
                schema: "claims",
                table: "claims");

            migrationBuilder.DropColumn(
                name: "decided_at_utc",
                schema: "claims",
                table: "claims");

            migrationBuilder.DropColumn(
                name: "decided_by_user_id",
                schema: "claims",
                table: "claims");

            migrationBuilder.DropColumn(
                name: "denial_narrative",
                schema: "claims",
                table: "claims");

            migrationBuilder.DropColumn(
                name: "denial_reason",
                schema: "claims",
                table: "claims");

            migrationBuilder.DropColumn(
                name: "settlement_amount",
                schema: "claims",
                table: "claims");
        }
    }
}
