using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.NotificationService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplateMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "display_name",
                table: "notification_templates",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "notification_templates",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "subject_template",
                table: "notification_templates",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "display_name",
                table: "notification_templates");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "notification_templates");

            migrationBuilder.DropColumn(
                name: "subject_template",
                table: "notification_templates");
        }
    }
}
