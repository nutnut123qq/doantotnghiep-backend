using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace StockInvestment.Application.Features.Auth.VerifyEmail;

public class VerifyEmailHandler : IRequestHandler<VerifyEmailCommand, VerifyEmailDto>
{
    private readonly IEmailVerificationTokenRepository _tokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<VerifyEmailHandler> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _jwtSecret;
    private readonly string _jwtIssuer;
    private readonly string _jwtAudience;

    public VerifyEmailHandler(
        IEmailVerificationTokenRepository tokenRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ILogger<VerifyEmailHandler> logger,
        IConfiguration configuration)
    {
        _tokenRepository = tokenRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _configuration = configuration;

        _jwtSecret = configuration["JWT:Secret"]
            ?? throw new InvalidOperationException("JWT:Secret is required in configuration");

        if (_jwtSecret.Length < 32)
        {
            throw new InvalidOperationException($"JWT:Secret must be at least 32 characters long. Current length: {_jwtSecret.Length}");
        }

        _jwtIssuer = configuration["JWT:Issuer"] ?? "StockInvestmentApi";
        _jwtAudience = configuration["JWT:Audience"] ?? "StockInvestmentClient";
    }

    public async Task<VerifyEmailDto> Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
    {
        var token = await _tokenRepository.GetByTokenAsync(request.Token, cancellationToken);

        if (token == null)
        {
            return new VerifyEmailDto
            {
                Success = false,
                Message = "Invalid verification token."
            };
        }

        if (token.IsUsed)
        {
            return new VerifyEmailDto
            {
                Success = false,
                Message = "This verification token has already been used."
            };
        }

        if (token.IsExpired)
        {
            return new VerifyEmailDto
            {
                Success = false,
                Message = "This verification token has expired. Please request a new one."
            };
        }

        var user = await _userRepository.GetByIdAsync(token.UserId, cancellationToken);
        if (user == null)
        {
            return new VerifyEmailDto
            {
                Success = false,
                Message = "User not found."
            };
        }

        token.IsUsed = true;
        token.UsedAt = DateTime.UtcNow;

        user.IsEmailVerified = true;
        user.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Email verified for user {UserId} ({Email})", user.Id, user.Email.Value);

        var jwt = GenerateJwtToken(user);

        return new VerifyEmailDto
        {
            Success = true,
            Message = "Email verified successfully. You are now logged in.",
            Token = jwt,
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
            Issuer = _jwtIssuer,
            Audience = _jwtAudience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
