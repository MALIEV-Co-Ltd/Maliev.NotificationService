using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.NotificationService.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "channel_bindings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    channel_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    channel_identifier = table.Column<string>(type: "text", nullable: false),
                    is_valid = table.Column<bool>(type: "boolean", nullable: false),
                    invalidated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    invalidated_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_channel_bindings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "dead_letter_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    event_payload = table.Column<string>(type: "text", nullable: false),
                    failure_reasons = table.Column<string>(type: "text", nullable: false),
                    total_attempts = table.Column<int>(type: "integer", nullable: false),
                    escalation_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    escalated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dead_letter_records", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "delivery_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    channel_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    recipient_identifier = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    message_content = table.Column<string>(type: "text", nullable: true),
                    provider_response = table.Column<string>(type: "text", nullable: true),
                    provider_message_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    attempt_number = table.Column<int>(type: "integer", nullable: false),
                    delivered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_delivery_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notification_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    channel_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    content_template = table.Column<string>(type: "text", nullable: false),
                    parameters = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "retry_queue_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    event_payload = table.Column<string>(type: "text", nullable: false),
                    attempt_number = table.Column<int>(type: "integer", nullable: false),
                    scheduled_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_retry_queue_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_notification_preferences",
                columns: table => new
                {
                    user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    primary_channel_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fallback_channel_types = table.Column<string>(type: "jsonb", nullable: false),
                    opt_out_categories = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_notification_preferences", x => x.user_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_channel_bindings_is_valid",
                table: "channel_bindings",
                column: "is_valid");

            migrationBuilder.CreateIndex(
                name: "ix_channel_bindings_user_id",
                table: "channel_bindings",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_channel_bindings_user_id_channel_type",
                table: "channel_bindings",
                columns: new[] { "user_id", "channel_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_dead_letter_records_created_at",
                table: "dead_letter_records",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_dead_letter_records_escalation_status",
                table: "dead_letter_records",
                column: "escalation_status");

            migrationBuilder.CreateIndex(
                name: "ix_dead_letter_records_event_id",
                table: "dead_letter_records",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_logs_created_at",
                table: "delivery_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_logs_event_id",
                table: "delivery_logs",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_logs_status",
                table: "delivery_logs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_logs_user_id",
                table: "delivery_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_templates_template_key",
                table: "notification_templates",
                column: "template_key");

            migrationBuilder.CreateIndex(
                name: "ix_notification_templates_template_key_version_language_channe~",
                table: "notification_templates",
                columns: new[] { "template_key", "version", "language", "channel_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_retry_queue_entries_event_id",
                table: "retry_queue_entries",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "ix_retry_queue_entries_scheduled_time",
                table: "retry_queue_entries",
                column: "scheduled_time");

            migrationBuilder.CreateIndex(
                name: "ix_user_notification_preferences_primary_channel_type",
                table: "user_notification_preferences",
                column: "primary_channel_type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "channel_bindings");

            migrationBuilder.DropTable(
                name: "dead_letter_records");

            migrationBuilder.DropTable(
                name: "delivery_logs");

            migrationBuilder.DropTable(
                name: "notification_templates");

            migrationBuilder.DropTable(
                name: "retry_queue_entries");

            migrationBuilder.DropTable(
                name: "user_notification_preferences");
        }
    }
}
