using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LIAnsureProtect.Modules.Underwriting.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateReferralOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "quote_referral_operations",
                schema: "underwriting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    quote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_underwriter_user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    priority = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    due_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    closed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quote_referral_operations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "referral_operation_projected_messages",
                schema: "underwriting",
                columns: table => new
                {
                    source_outbox_message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    applied_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_referral_operation_projected_messages", x => x.source_outbox_message_id);
                });

            migrationBuilder.CreateTable(
                name: "quote_referral_follow_up_tasks",
                schema: "underwriting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    quote_referral_operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    due_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_by_user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quote_referral_follow_up_tasks", x => x.id);
                    table.ForeignKey(
                        name: "FK_quote_referral_follow_up_tasks_quote_referral_operations_qu~",
                        column: x => x.quote_referral_operation_id,
                        principalSchema: "underwriting",
                        principalTable: "quote_referral_operations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "quote_referral_timeline_entries",
                schema: "underwriting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    quote_referral_operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    summary = table.Column<string>(type: "text", nullable: false),
                    created_by_user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quote_referral_timeline_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_quote_referral_timeline_entries_quote_referral_operations_q~",
                        column: x => x.quote_referral_operation_id,
                        principalSchema: "underwriting",
                        principalTable: "quote_referral_operations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "quote_referral_work_notes",
                schema: "underwriting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    quote_referral_operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note = table.Column<string>(type: "text", nullable: false),
                    created_by_user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quote_referral_work_notes", x => x.id);
                    table.ForeignKey(
                        name: "FK_quote_referral_work_notes_quote_referral_operations_quote_r~",
                        column: x => x.quote_referral_operation_id,
                        principalSchema: "underwriting",
                        principalTable: "quote_referral_operations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_quote_referral_follow_up_tasks_quote_id_completed_due_at_utc",
                schema: "underwriting",
                table: "quote_referral_follow_up_tasks",
                columns: new[] { "quote_id", "completed_at_utc", "due_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_quote_referral_follow_up_tasks_quote_referral_operation_id",
                schema: "underwriting",
                table: "quote_referral_follow_up_tasks",
                column: "quote_referral_operation_id");

            migrationBuilder.CreateIndex(
                name: "ix_quote_referral_operations_status_priority_due_at_utc",
                schema: "underwriting",
                table: "quote_referral_operations",
                columns: new[] { "status", "priority", "due_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_quote_referral_operations_quote_id",
                schema: "underwriting",
                table: "quote_referral_operations",
                column: "quote_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_quote_referral_timeline_entries_quote_id_created_at_utc",
                schema: "underwriting",
                table: "quote_referral_timeline_entries",
                columns: new[] { "quote_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_quote_referral_timeline_entries_quote_referral_operation_id",
                schema: "underwriting",
                table: "quote_referral_timeline_entries",
                column: "quote_referral_operation_id");

            migrationBuilder.CreateIndex(
                name: "ix_quote_referral_work_notes_quote_id_created_at_utc",
                schema: "underwriting",
                table: "quote_referral_work_notes",
                columns: new[] { "quote_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_quote_referral_work_notes_quote_referral_operation_id",
                schema: "underwriting",
                table: "quote_referral_work_notes",
                column: "quote_referral_operation_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quote_referral_follow_up_tasks",
                schema: "underwriting");

            migrationBuilder.DropTable(
                name: "quote_referral_timeline_entries",
                schema: "underwriting");

            migrationBuilder.DropTable(
                name: "quote_referral_work_notes",
                schema: "underwriting");

            migrationBuilder.DropTable(
                name: "referral_operation_projected_messages",
                schema: "underwriting");

            migrationBuilder.DropTable(
                name: "quote_referral_operations",
                schema: "underwriting");
        }
    }
}
