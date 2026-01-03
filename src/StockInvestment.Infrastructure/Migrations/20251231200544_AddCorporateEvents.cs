using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockInvestment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCorporateEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CorporateEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StockTickerId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    EventDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    SourceUrl = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AdditionalData = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: true),
                    MeetingTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    Agenda = table.Column<string>(type: "text", nullable: true),
                    RecordDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Year = table.Column<int>(type: "integer", nullable: true),
                    DividendPerShare = table.Column<decimal>(type: "numeric", nullable: true),
                    CashDividend = table.Column<decimal>(type: "numeric", nullable: true),
                    StockDividendRatio = table.Column<decimal>(type: "numeric", nullable: true),
                    ExDividendDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DividendEvent_RecordDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaymentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Period = table.Column<string>(type: "text", nullable: true),
                    EarningsEvent_Year = table.Column<int>(type: "integer", nullable: true),
                    EPS = table.Column<decimal>(type: "numeric", nullable: true),
                    Revenue = table.Column<decimal>(type: "numeric", nullable: true),
                    NetProfit = table.Column<decimal>(type: "numeric", nullable: true),
                    NumberOfShares = table.Column<long>(type: "bigint", nullable: true),
                    IssuePrice = table.Column<decimal>(type: "numeric", nullable: true),
                    RightsRatio = table.Column<string>(type: "text", nullable: true),
                    RightsIssueEvent_RecordDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubscriptionStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubscriptionEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Purpose = table.Column<string>(type: "text", nullable: true),
                    SplitRatio = table.Column<string>(type: "text", nullable: true),
                    IsReverseSplit = table.Column<bool>(type: "boolean", nullable: true),
                    EffectiveDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StockSplitEvent_RecordDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorporateEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorporateEvents_StockTickers_StockTickerId",
                        column: x => x.StockTickerId,
                        principalTable: "StockTickers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CorporateEvents_EventDate",
                table: "CorporateEvents",
                column: "EventDate");

            migrationBuilder.CreateIndex(
                name: "IX_CorporateEvents_StockTickerId",
                table: "CorporateEvents",
                column: "StockTickerId");

            migrationBuilder.CreateIndex(
                name: "IX_CorporateEvents_StockTickerId_EventDate",
                table: "CorporateEvents",
                columns: new[] { "StockTickerId", "EventDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CorporateEvents");
        }
    }
}
