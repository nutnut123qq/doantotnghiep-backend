using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockInvestment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropLegacyRowVersionOnNotificationChannelConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "NotificationChannelConfigs"
                DROP COLUMN IF EXISTS "RowVersion";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "NotificationChannelConfigs"
                ADD COLUMN IF NOT EXISTS "RowVersion" bytea NOT NULL DEFAULT '\x'::bytea;
                """);
        }
    }
}
