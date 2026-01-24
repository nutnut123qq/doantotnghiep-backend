using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockInvestment.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// P1-1: Adds IX_Alerts_IsActive_TriggeredAt for AlertMonitorJob queries.
    /// AddPerformanceIndexes only created IX_Alerts_UserId_IsActive and IX_Alerts_TickerId_IsActive;
    /// this index was missing from migrations.
    /// </summary>
    public class AddAlertIsActiveTriggeredAtIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Alerts_IsActive_TriggeredAt",
                table: "Alerts",
                columns: new[] { "IsActive", "TriggeredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Alerts_IsActive_TriggeredAt",
                table: "Alerts");
        }
    }
}
