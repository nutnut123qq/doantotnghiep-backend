using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockInvestment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Indexes for StockTickers table (most queried)
            migrationBuilder.CreateIndex(
                name: "IX_StockTickers_Symbol",
                table: "StockTickers",
                column: "Symbol",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockTickers_Exchange",
                table: "StockTickers",
                column: "Exchange");

            migrationBuilder.CreateIndex(
                name: "IX_StockTickers_LastUpdated",
                table: "StockTickers",
                column: "LastUpdated");

            // Indexes for Alerts table
            migrationBuilder.CreateIndex(
                name: "IX_Alerts_UserId_IsActive",
                table: "Alerts",
                columns: new[] { "UserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_TickerId_IsActive",
                table: "Alerts",
                columns: new[] { "TickerId", "IsActive" });

            // Indexes for News table
            // ⚠️ WARNING: This migration uses "PublishedDate" which is INCORRECT
            // The actual column name is "PublishedAt" (see News.cs entity)
            // This migration should NOT be applied - use 20260126000000_AddNewsIndexes.cs instead
            // Keeping this for reference only - DO NOT RUN
            // migrationBuilder.CreateIndex(
            //     name: "IX_News_PublishedDate",
            //     table: "News",
            //     column: "PublishedDate",
            //     descending: new bool[] { true });

            migrationBuilder.CreateIndex(
                name: "IX_News_Source",
                table: "News",
                column: "Source");

            // Indexes for CorporateEvents table
            migrationBuilder.CreateIndex(
                name: "IX_CorporateEvents_TickerId_EventDate",
                table: "CorporateEvents",
                columns: new[] { "TickerId", "EventDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CorporateEvents_EventType",
                table: "CorporateEvents",
                column: "EventType");

            // Indexes for TechnicalIndicators table
            migrationBuilder.CreateIndex(
                name: "IX_TechnicalIndicators_TickerId_Date",
                table: "TechnicalIndicators",
                columns: new[] { "TickerId", "Date" });

            // Indexes for AnalyticsEvents table
            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsEvents_EventType_CreatedAt",
                table: "AnalyticsEvents",
                columns: new[] { "EventType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsEvents_Symbol",
                table: "AnalyticsEvents",
                column: "Symbol");

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsEvents_UserId_CreatedAt",
                table: "AnalyticsEvents",
                columns: new[] { "UserId", "CreatedAt" });

            // Indexes for Watchlists table
            migrationBuilder.CreateIndex(
                name: "IX_Watchlists_UserId_CreatedAt",
                table: "Watchlists",
                columns: new[] { "UserId", "CreatedAt" });

            // Indexes for FinancialReports table
            migrationBuilder.CreateIndex(
                name: "IX_FinancialReports_Symbol_Year_Quarter",
                table: "FinancialReports",
                columns: new[] { "Symbol", "Year", "Quarter" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop all indexes
            migrationBuilder.DropIndex(name: "IX_StockTickers_Symbol", table: "StockTickers");
            migrationBuilder.DropIndex(name: "IX_StockTickers_Exchange", table: "StockTickers");
            migrationBuilder.DropIndex(name: "IX_StockTickers_LastUpdated", table: "StockTickers");
            
            migrationBuilder.DropIndex(name: "IX_Alerts_UserId_IsActive", table: "Alerts");
            migrationBuilder.DropIndex(name: "IX_Alerts_TickerId_IsActive", table: "Alerts");
            
            migrationBuilder.DropIndex(name: "IX_News_PublishedDate", table: "News");
            migrationBuilder.DropIndex(name: "IX_News_Source", table: "News");
            
            migrationBuilder.DropIndex(name: "IX_CorporateEvents_TickerId_EventDate", table: "CorporateEvents");
            migrationBuilder.DropIndex(name: "IX_CorporateEvents_EventType", table: "CorporateEvents");
            
            migrationBuilder.DropIndex(name: "IX_TechnicalIndicators_TickerId_Date", table: "TechnicalIndicators");
            
            migrationBuilder.DropIndex(name: "IX_AnalyticsEvents_EventType_CreatedAt", table: "AnalyticsEvents");
            migrationBuilder.DropIndex(name: "IX_AnalyticsEvents_Symbol", table: "AnalyticsEvents");
            migrationBuilder.DropIndex(name: "IX_AnalyticsEvents_UserId_CreatedAt", table: "AnalyticsEvents");
            
            migrationBuilder.DropIndex(name: "IX_Watchlists_UserId_CreatedAt", table: "Watchlists");
            
            migrationBuilder.DropIndex(name: "IX_FinancialReports_Symbol_Year_Quarter", table: "FinancialReports");
        }
    }
}
