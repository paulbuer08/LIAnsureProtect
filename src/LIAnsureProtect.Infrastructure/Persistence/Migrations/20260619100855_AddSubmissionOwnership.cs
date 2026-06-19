using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LIAnsureProtect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubmissionOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "owner_user_id",
                table: "submissions",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.Sql("update submissions set owner_user_id = 'legacy-unassigned' where owner_user_id is null;");

            migrationBuilder.AlterColumn<string>(
                name: "owner_user_id",
                table: "submissions",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_submissions_owner_user_id_created_at_utc",
                table: "submissions",
                columns: new[] { "owner_user_id", "created_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_submissions_owner_user_id_created_at_utc",
                table: "submissions");

            migrationBuilder.DropColumn(
                name: "owner_user_id",
                table: "submissions");
        }
    }
}
