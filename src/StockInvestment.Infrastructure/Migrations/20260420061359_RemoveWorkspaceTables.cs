using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockInvestment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveWorkspaceTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""Layouts"" DROP CONSTRAINT IF EXISTS ""FK_Layouts_Workspaces_WorkspaceId"";
                DROP TABLE IF EXISTS ""WorkspaceLayouts"";
                DROP TABLE IF EXISTS ""WorkspaceMembers"";
                DROP TABLE IF EXISTS ""WorkspaceMessages"";
                DROP TABLE IF EXISTS ""WorkspaceWatchlists"";
                DROP TABLE IF EXISTS ""Workspaces"";
                DROP INDEX IF EXISTS ""IX_Layouts_WorkspaceId"";
                ALTER TABLE ""Layouts"" DROP COLUMN IF EXISTS ""WorkspaceId"";
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
