using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.NotificationService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeduplicationEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "deduplication_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_deduplication_entries", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_deduplication_entries_event_id",
                table: "deduplication_entries",
                column: "event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_deduplication_entries_expires_at",
                table: "deduplication_entries",
                column: "expires_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "deduplication_entries");
        }
    }
}
