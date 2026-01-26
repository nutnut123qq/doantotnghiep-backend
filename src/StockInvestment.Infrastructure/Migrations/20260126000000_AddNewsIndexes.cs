using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockInvestment.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// P1-1: Adds indexes for News table:
    /// - Unique index on News.Url (case-insensitive) for deduplication
    /// - Index on News.PublishedAt for query performance
    /// </summary>
    public partial class AddNewsIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // P1-1: Create unique index on News.Url (case-insensitive using lower())
            // This prevents duplicate news articles across multiple instances
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_News_Url_Unique""
                ON ""News"" (lower(""Url""))
                WHERE ""Url"" IS NOT NULL;
            ");

            // P1-1: Create index on News.PublishedAt for sorting/filtering queries
            migrationBuilder.CreateIndex(
                name: "IX_News_PublishedAt",
                table: "News",
                column: "PublishedAt",
                descending: new bool[] { true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_News_PublishedAt",
                table: "News");

            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ""IX_News_Url_Unique"";
            ");
        }
    }
}
