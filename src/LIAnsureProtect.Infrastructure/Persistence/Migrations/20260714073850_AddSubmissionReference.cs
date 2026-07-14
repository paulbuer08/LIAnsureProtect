using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LIAnsureProtect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubmissionReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "reference",
                table: "submissions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE submissions
                SET reference = 'SUB-'
                    || EXTRACT(YEAR FROM created_at_utc AT TIME ZONE 'UTC')::integer
                    || '-'
                    || UPPER(SUBSTRING(REPLACE(id::text, '-', '') FROM 1 FOR 16));
                """);

            migrationBuilder.AlterColumn<string>(
                name: "reference",
                table: "submissions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_submissions_reference",
                table: "submissions",
                column: "reference",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_submissions_reference",
                table: "submissions");

            migrationBuilder.DropColumn(
                name: "reference",
                table: "submissions");
        }
    }
}
