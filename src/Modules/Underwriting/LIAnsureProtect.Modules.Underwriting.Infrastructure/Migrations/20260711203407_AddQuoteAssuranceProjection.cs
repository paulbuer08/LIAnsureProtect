using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LIAnsureProtect.Modules.Underwriting.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteAssuranceProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "quote_assurance_projected_messages",
                schema: "underwriting",
                columns: table => new
                {
                    source_outbox_message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    projected_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quote_assurance_projected_messages", x => x.source_outbox_message_id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quote_assurance_projected_messages",
                schema: "underwriting");
        }
    }
}
