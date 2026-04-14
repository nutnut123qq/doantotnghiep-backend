using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockInvestment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDeleteForFinancialReportsAndCorporateEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "FinancialReports",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "CorporateEvents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_FinancialReports_IsDeleted",
                table: "FinancialReports",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_CorporateEvents_IsDeleted",
                table: "CorporateEvents",
                column: "IsDeleted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FinancialReports_IsDeleted",
                table: "FinancialReports");

            migrationBuilder.DropIndex(
                name: "IX_CorporateEvents_IsDeleted",
                table: "CorporateEvents");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "FinancialReports");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "CorporateEvents");
        }
    }
}
