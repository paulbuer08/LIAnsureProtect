using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LIAnsureProtect.Modules.Claims.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "claim_documents",
                schema: "claims",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    content_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    uploaded_by_user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    uploaded_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    scan_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    scanner_provider_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    scan_result_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    scan_result_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    scanned_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_claim_documents", x => x.id);
                    table.ForeignKey(
                        name: "FK_claim_documents_claims_claim_id",
                        column: x => x.claim_id,
                        principalSchema: "claims",
                        principalTable: "claims",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_claim_documents_claim_id_uploaded_at_utc",
                schema: "claims",
                table: "claim_documents",
                columns: new[] { "claim_id", "uploaded_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "claim_documents",
                schema: "claims");
        }
    }
}
