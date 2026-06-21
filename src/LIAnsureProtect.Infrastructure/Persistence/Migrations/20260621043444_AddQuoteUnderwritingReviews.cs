using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LIAnsureProtect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteUnderwritingReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "reviewed_at_utc",
                table: "quotes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reviewed_by_user_id",
                table: "quotes",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "underwriting_decision_notes",
                table: "quotes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "underwriting_decision_reason",
                table: "quotes",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "quote_underwriting_reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    quote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decision = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reviewed_by_user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    premium_before = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    premium_after = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    retention_before = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    retention_after = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quote_underwriting_reviews", x => x.id);
                    table.ForeignKey(
                        name: "FK_quote_underwriting_reviews_quotes_quote_id",
                        column: x => x.quote_id,
                        principalTable: "quotes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_quotes_status_created_at_utc",
                table: "quotes",
                columns: new[] { "status", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_quote_underwriting_reviews_quote_id_created_at_utc",
                table: "quote_underwriting_reviews",
                columns: new[] { "quote_id", "created_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quote_underwriting_reviews");

            migrationBuilder.DropIndex(
                name: "ix_quotes_status_created_at_utc",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "reviewed_at_utc",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "reviewed_by_user_id",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "underwriting_decision_notes",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "underwriting_decision_reason",
                table: "quotes");
        }
    }
}
