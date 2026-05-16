using Npgsql;

var cs =
    Environment.GetEnvironmentVariable("STOCK_DB")
    ?? "Host=localhost;Port=5432;Database=stock_investment_dev;Username=postgres;Password=123456";

await using var conn = new NpgsqlConnection(cs);
await conn.OpenAsync();

Console.WriteLine("=== All StockTickers (ordered by LastUpdated desc) ===");
{
    await using var cmd = new NpgsqlCommand(
        @"SELECT ""Symbol"", ""LastUpdated"", ""Id""
          FROM ""StockTickers""
          ORDER BY ""LastUpdated"" DESC NULLS LAST, ""Symbol""", conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    int i = 1;
    while (await reader.ReadAsync())
    {
        var sym = reader.GetString(0);
        var lastUp = reader.IsDBNull(1) ? "NULL" : reader.GetDateTime(1).ToString("yyyy-MM-dd HH:mm");
        Console.WriteLine($"{i,2}. {sym,-4} LastUpdated={lastUp}");
        i++;
    }
}
