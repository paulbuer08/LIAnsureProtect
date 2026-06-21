using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LIAnsureProtect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteRatingProviderAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "quote_rating_provider_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    quote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    market_disposition = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    provider_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    provider_quote_number = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    indicated_premium = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    indicated_limit = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    indicated_retention = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    http_status_code = table.Column<int>(type: "integer", nullable: true),
                    failure_category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    failure_reason = table.Column<string>(type: "text", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    duration_ms = table.Column<long>(type: "bigint", nullable: false),
                    request_payload_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quote_rating_provider_attempts", x => x.id);
                    table.ForeignKey(
                        name: "FK_quote_rating_provider_attempts_quotes_quote_id",
                        column: x => x.quote_id,
                        principalTable: "quotes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_quote_rating_provider_attempts_quote_id_created_at_utc",
                table: "quote_rating_provider_attempts",
                columns: new[] { "quote_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_quote_rating_provider_attempts_status_created_at_utc",
                table: "quote_rating_provider_attempts",
                columns: new[] { "status", "created_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quote_rating_provider_attempts");
        }
    }
}
