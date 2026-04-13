using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace StockInvestment.Api.Tests.Helpers;

/// <summary>
/// Builds JWT tokens for integration tests (same secret/issuer/audience as API in Testing).
/// </summary>
public static class TestJwtHelper
{
    private const string Secret = "integration-test-secret-key-at-least-32-characters-long";
    private const string Issuer = "StockInvestmentApi";
    private const string Audience = "StockInvestmentClient";

    public static string CreateToken(Guid userId, string email, string role = TestUserConstants.TestUserRole)
    {
        var key = Encoding.ASCII.GetBytes(Secret);
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Role, role)
            }),
            Expires = DateTime.UtcNow.AddHours(1),
            Issuer = Issuer,
            Audience = Audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
