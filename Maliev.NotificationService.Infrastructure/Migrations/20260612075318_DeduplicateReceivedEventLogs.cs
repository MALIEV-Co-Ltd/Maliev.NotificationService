using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.NotificationService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DeduplicateReceivedEventLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_delivery_logs_event_id_user_id",
                table: "delivery_logs");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_logs_event_id_user_id",
                table: "delivery_logs",
                columns: new[] { "event_id", "user_id" },
                unique: true,
                filter: "\"status\" = 'received'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_delivery_logs_event_id_user_id",
                table: "delivery_logs");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_logs_event_id_user_id",
                table: "delivery_logs",
                columns: new[] { "event_id", "user_id" },
                unique: true,
                filter: "\"status\" = 'delivered'");
        }
    }
}
