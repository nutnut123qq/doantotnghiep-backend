using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockInvestment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class refactorbackendver1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Alerts_TickerId",
                table: "Alerts");

            migrationBuilder.DropIndex(
                name: "IX_Alerts_UserId",
                table: "Alerts");

            migrationBuilder.CreateIndex(
                name: "IX_News_PublishedAt",
                table: "News",
                column: "PublishedAt");

            migrationBuilder.CreateIndex(
                name: "IX_News_Url_Unique",
                table: "News",
                column: "Url",
                unique: true,
                filter: "\"Url\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_TickerId_IsActive",
                table: "Alerts",
                columns: new[] { "TickerId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_UserId_IsActive",
                table: "Alerts",
                columns: new[] { "UserId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_News_PublishedAt",
                table: "News");

            migrationBuilder.DropIndex(
                name: "IX_News_Url_Unique",
                table: "News");

            migrationBuilder.DropIndex(
                name: "IX_Alerts_TickerId_IsActive",
                table: "Alerts");

            migrationBuilder.DropIndex(
                name: "IX_Alerts_UserId_IsActive",
                table: "Alerts");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_TickerId",
                table: "Alerts",
                column: "TickerId");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_UserId",
                table: "Alerts",
                column: "UserId");
        }
    }
}
