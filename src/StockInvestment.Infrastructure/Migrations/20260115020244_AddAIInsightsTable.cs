using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockInvestment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAIInsightsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AIInsights",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TickerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Confidence = table.Column<int>(type: "integer", nullable: false),
                    Reasoning = table.Column<string>(type: "text", nullable: false),
                    TargetPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    StopLoss = table.Column<decimal>(type: "numeric", nullable: true),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DismissedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DismissedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIInsights", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AIInsights_StockTickers_TickerId",
                        column: x => x.TickerId,
                        principalTable: "StockTickers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AIInsights_Users_DismissedByUserId",
                        column: x => x.DismissedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AIInsights_DismissedAt",
                table: "AIInsights",
                column: "DismissedAt",
                filter: "\"DismissedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AIInsights_DismissedByUserId",
                table: "AIInsights",
                column: "DismissedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AIInsights_GeneratedAt",
                table: "AIInsights",
                column: "GeneratedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AIInsights_TickerId",
                table: "AIInsights",
                column: "TickerId");

            migrationBuilder.CreateIndex(
                name: "IX_AIInsights_TickerId_Type_GeneratedAt",
                table: "AIInsights",
                columns: new[] { "TickerId", "Type", "GeneratedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AIInsights_Type",
                table: "AIInsights",
                column: "Type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AIInsights");
        }
    }
}
