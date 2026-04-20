using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockInvestment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAnalysisReportsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalysisReports");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnalysisReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FirmName = table.Column<string>(type: "text", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Recommendation = table.Column<string>(type: "text", nullable: true),
                    SourceUrl = table.Column<string>(type: "text", nullable: true),
                    Symbol = table.Column<string>(type: "text", nullable: false),
                    TargetPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisReports", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisReports_PublishedAt",
                table: "AnalysisReports",
                column: "PublishedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisReports_Symbol",
                table: "AnalysisReports",
                column: "Symbol");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisReports_Symbol_PublishedAt",
                table: "AnalysisReports",
                columns: new[] { "Symbol", "PublishedAt" },
                descending: new[] { false, true });
        }
    }
}
