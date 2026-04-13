using Npgsql;

var cs =
    Environment.GetEnvironmentVariable("STOCK_DB")
    ?? "Host=localhost;Port=5432;Database=stock_investment_dev;Username=postgres;Password=123456";

await using var conn = new NpgsqlConnection(cs);
await conn.OpenAsync();
await using var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM \"CorporateEvents\"", conn);
var n = await cmd.ExecuteScalarAsync();
Console.WriteLine(n);
