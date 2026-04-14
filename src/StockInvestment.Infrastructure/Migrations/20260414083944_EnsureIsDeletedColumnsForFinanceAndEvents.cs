using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockInvestment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnsureIsDeletedColumnsForFinanceAndEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE \"FinancialReports\" ADD COLUMN IF NOT EXISTS \"IsDeleted\" boolean NOT NULL DEFAULT FALSE;");

            migrationBuilder.Sql(
                "ALTER TABLE \"CorporateEvents\" ADD COLUMN IF NOT EXISTS \"IsDeleted\" boolean NOT NULL DEFAULT FALSE;");

            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS \"IX_FinancialReports_IsDeleted\" ON \"FinancialReports\" (\"IsDeleted\");");

            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS \"IX_CorporateEvents_IsDeleted\" ON \"CorporateEvents\" (\"IsDeleted\");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_FinancialReports_IsDeleted\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_CorporateEvents_IsDeleted\";");
            migrationBuilder.Sql("ALTER TABLE \"FinancialReports\" DROP COLUMN IF EXISTS \"IsDeleted\";");
            migrationBuilder.Sql("ALTER TABLE \"CorporateEvents\" DROP COLUMN IF EXISTS \"IsDeleted\";");
        }
    }
}
