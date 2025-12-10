using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using StockInvestment.Domain.Entities;
using StockInvestment.Domain.ValueObjects;
using StockInvestment.Domain.Exceptions;
using StockInvestment.Application.Interfaces;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace StockInvestment.Application.Features.Auth.Login;

public class LoginHandler : IRequestHandler<LoginCommand, LoginDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<LoginHandler> _logger;
    private readonly string _jwtSecret;

    public LoginHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ILogger<LoginHandler> logger,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _logger = logger;
        _jwtSecret = configuration["JWT:Secret"] ?? "your-super-secret-key-change-in-production-min-32-chars";
    }

    public async Task<LoginDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var email = Email.Create(request.Email);
        var user = await _userRepository.GetByEmailAsync(email, cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("Login attempt failed: User not found for email {Email}", email.Value);
            throw new UnauthorizedException("Invalid email or password");
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Login attempt failed: Account deactivated for email {Email}", email.Value);
            throw new UnauthorizedException("Account is deactivated");
        }

        // Verify password using BCrypt
        if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Login attempt failed: Invalid password for email {Email}", email.Value);
            throw new UnauthorizedException("Invalid email or password");
        }

        // Generate JWT token
        var token = GenerateJwtToken(user);

        _logger.LogInformation("User logged in successfully: {Email} | UserId: {UserId}", email.Value, user.Id);

        return new LoginDto
        {
            Token = token,
            Email = user.Email.Value,
            Role = user.Role.ToString()
        };
    }

    private string GenerateJwtToken(User user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_jwtSecret);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email.Value),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            }),
            Expires = DateTime.UtcNow.AddDays(7),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}

