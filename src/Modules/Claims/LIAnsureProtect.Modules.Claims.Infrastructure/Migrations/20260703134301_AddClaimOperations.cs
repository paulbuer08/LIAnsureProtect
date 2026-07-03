using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LIAnsureProtect.Modules.Claims.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "assigned_adjuster_user_id",
                schema: "claims",
                table: "claims",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "claim_information_requests",
                schema: "claims",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    requested_by_user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    requested_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_answered = table.Column<bool>(type: "boolean", nullable: false),
                    response_text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    responded_by_user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    responded_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_claim_information_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_claim_information_requests_claims_claim_id",
                        column: x => x.claim_id,
                        principalSchema: "claims",
                        principalTable: "claims",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "claim_work_notes",
                schema: "claims",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    created_by_user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_claim_work_notes", x => x.id);
                    table.ForeignKey(
                        name: "FK_claim_work_notes_claims_claim_id",
                        column: x => x.claim_id,
                        principalSchema: "claims",
                        principalTable: "claims",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_claims_assigned_adjuster_user_id",
                schema: "claims",
                table: "claims",
                column: "assigned_adjuster_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_claim_information_requests_claim_id_requested_at_utc",
                schema: "claims",
                table: "claim_information_requests",
                columns: new[] { "claim_id", "requested_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_claim_work_notes_claim_id_created_at_utc",
                schema: "claims",
                table: "claim_work_notes",
                columns: new[] { "claim_id", "created_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "claim_information_requests",
                schema: "claims");

            migrationBuilder.DropTable(
                name: "claim_work_notes",
                schema: "claims");

            migrationBuilder.DropIndex(
                name: "ix_claims_assigned_adjuster_user_id",
                schema: "claims",
                table: "claims");

            migrationBuilder.DropColumn(
                name: "assigned_adjuster_user_id",
                schema: "claims",
                table: "claims");
        }
    }
}
