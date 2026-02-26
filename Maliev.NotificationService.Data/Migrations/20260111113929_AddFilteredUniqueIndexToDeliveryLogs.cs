using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.NotificationService.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFilteredUniqueIndexToDeliveryLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_delivery_logs_event_id_user_id",
                table: "delivery_logs",
                columns: new[] { "event_id", "user_id" },
                unique: true,
                filter: "\"status\" = 'delivered'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_delivery_logs_event_id_user_id",
                table: "delivery_logs");
        }
    }
}
