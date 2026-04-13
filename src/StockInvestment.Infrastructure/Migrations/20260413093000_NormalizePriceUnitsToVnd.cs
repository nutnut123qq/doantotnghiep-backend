using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockInvestment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NormalizePriceUnitsToVnd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Normalize StockTickers currently stored in thousand-VND to VND.
            migrationBuilder.Sql(
                """
                UPDATE "StockTickers"
                SET
                    "CurrentPrice" = "CurrentPrice" * 1000,
                    "PreviousClose" = CASE WHEN "PreviousClose" IS NOT NULL THEN "PreviousClose" * 1000 ELSE NULL END,
                    "Change" = CASE WHEN "Change" IS NOT NULL THEN "Change" * 1000 ELSE NULL END,
                    "Value" = CASE WHEN "Value" IS NOT NULL THEN "Value" * 1000 ELSE NULL END
                WHERE "CurrentPrice" > 0 AND "CurrentPrice" < 1000;
                """);

            // 2) Normalize Portfolio.CurrentPrice where AvgPrice is already full VND.
            migrationBuilder.Sql(
                """
                UPDATE "Portfolios"
                SET "CurrentPrice" = "CurrentPrice" * 1000
                WHERE "CurrentPrice" > 0 AND "CurrentPrice" < 1000
                  AND "AvgPrice" >= 10000;
                """);

            // 3) Normalize Portfolio.AvgPrice where CurrentPrice is already full VND.
            migrationBuilder.Sql(
                """
                UPDATE "Portfolios"
                SET "AvgPrice" = "AvgPrice" * 1000
                WHERE "AvgPrice" > 0 AND "AvgPrice" < 1000
                  AND "CurrentPrice" >= 10000;
                """);

            // 4) Recompute derived portfolio fields after normalization.
            migrationBuilder.Sql(
                """
                UPDATE "Portfolios"
                SET
                    "Value" = "Shares" * "CurrentPrice",
                    "GainLoss" = ("Shares" * "CurrentPrice") - ("Shares" * "AvgPrice"),
                    "GainLossPercentage" = CASE
                        WHEN ("Shares" * "AvgPrice") > 0
                            THEN ((("Shares" * "CurrentPrice") - ("Shares" * "AvgPrice")) / ("Shares" * "AvgPrice")) * 100
                        ELSE 0
                    END
                WHERE "Shares" > 0;
                """);

            // 5) Normalize AI insight price targets when ticker baseline already in full VND.
            migrationBuilder.Sql(
                """
                UPDATE "AIInsights" ai
                SET
                    "TargetPrice" = CASE
                        WHEN ai."TargetPrice" IS NOT NULL AND ai."TargetPrice" > 0 AND ai."TargetPrice" < 1000
                            THEN ai."TargetPrice" * 1000
                        ELSE ai."TargetPrice"
                    END,
                    "StopLoss" = CASE
                        WHEN ai."StopLoss" IS NOT NULL AND ai."StopLoss" > 0 AND ai."StopLoss" < 1000
                            THEN ai."StopLoss" * 1000
                        ELSE ai."StopLoss"
                    END
                FROM "StockTickers" st
                WHERE ai."TickerId" = st."Id"
                  AND st."CurrentPrice" >= 10000;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // This data normalization is intentionally non-reversible.
        }
    }
}
