using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockInvestment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedVolumeAlertNotificationTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "NotificationTemplates" ("Id", "Name", "EventType", "Subject", "Body", "IsActive", "CreatedAt", "UpdatedAt")
                SELECT
                    gen_random_uuid(),
                    'ALERT_VOLUME_TRIGGERED',
                    5,
                    'Volume Alert: {Symbol}',
                    E'🔔 Volume Alert Triggered\n\nStock: {Symbol}\nCondition: Volume {Operator} {Threshold}\nCurrent Volume: {CurrentValue}\nTime: {Time}\n\n---\nStock Investment Platform',
                    true,
                    NOW(),
                    NOW()
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM "NotificationTemplates"
                    WHERE "EventType" = 5 AND "IsActive" = true
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "NotificationTemplates"
                WHERE "Name" = 'ALERT_VOLUME_TRIGGERED';
                """);
        }
    }
}
