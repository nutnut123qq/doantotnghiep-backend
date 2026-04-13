namespace StockInvestment.Api.Tests.Helpers;

/// <summary>
/// Fixed IDs and credentials for seeded test users (InMemory DB).
/// </summary>
public static class TestUserConstants
{
    public static readonly Guid TestUserId = new("11111111-1111-1111-1111-111111111111");
    public const string TestUserEmail = "test@test.local";
    public const string TestUserPassword = "Test123!@#";
    public const string TestUserRole = "Investor";

    public static readonly Guid AdminUserId = new("22222222-2222-2222-2222-222222222222");
    public const string AdminUserEmail = "admin@test.local";
    public const string AdminUserPassword = "Admin123!@#";
    public const string AdminUserRole = "Admin";
}
