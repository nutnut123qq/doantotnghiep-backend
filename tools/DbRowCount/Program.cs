using Npgsql;

var cs =
    Environment.GetEnvironmentVariable("STOCK_DB")
    ?? "Host=localhost;Port=5432;Database=stock_investment_dev;Username=postgres;Password=123456";

await using var conn = new NpgsqlConnection(cs);
await conn.OpenAsync();

Console.WriteLine("=== AI Insights created after 13:40 today ===");
{
    await using var cmd = new NpgsqlCommand(
        @"SELECT st.""Symbol"", fr.""GeneratedAt"", fr.""Title""
          FROM ""AIInsights"" fr
          JOIN ""StockTickers"" st ON fr.""TickerId"" = st.""Id""
          WHERE fr.""GeneratedAt"" >= '2026-05-16 13:40:00'
            AND fr.""IsDeleted"" = false
          ORDER BY fr.""GeneratedAt"" DESC", conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    int count = 0;
    while (await reader.ReadAsync())
    {
        var sym = reader.GetString(0);
        var gen = reader.GetDateTime(1);
        var title = reader.GetString(2);
        Console.WriteLine($"{gen:HH:mm:ss}  {sym,-5}  {title.Substring(0, Math.Min(50, title.Length))}...");
        count++;
    }
    Console.WriteLine($"\nTotal new insights since 13:40: {count}");
}
