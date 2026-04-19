using MediatR;
using Microsoft.Extensions.Logging;
using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;
using StockInvestment.Domain.ValueObjects;
using StockInvestment.Domain.Exceptions;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Application.Features.Auth.Register;

public class RegisterHandler : IRequestHandler<RegisterCommand, RegisterDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailService _emailService;
    private readonly IEmailVerificationTokenRepository _tokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RegisterHandler> _logger;

    public RegisterHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IEmailService emailService,
        IEmailVerificationTokenRepository tokenRepository,
        IUnitOfWork unitOfWork,
        ILogger<RegisterHandler> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _emailService = emailService;
        _tokenRepository = tokenRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<RegisterDto> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var email = Email.Create(request.Email);
        var existingUser = await _userRepository.GetByEmailAsync(email, cancellationToken);

        if (existingUser != null)
        {
            return await HandleExistingUserAsync(existingUser, request.Password, cancellationToken);
        }

        var user = new User
        {
            Email = email,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            Role = UserRole.Investor,
            IsEmailVerified = false,
            IsActive = true
        };

        await _userRepository.AddAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        await CreateTokenAndSendAsync(user, cancellationToken);

        _logger.LogInformation(
            "User registered successfully: {Email} | UserId: {UserId}",
            email.Value,
            user.Id);

        return new RegisterDto
        {
            UserId = user.Id,
            Email = user.Email.Value,
            Message = "Registration successful. Please check your email to verify your account."
        };
    }

    private async Task<RegisterDto> HandleExistingUserAsync(User existingUser, string password, CancellationToken cancellationToken)
    {
        if (existingUser.IsEmailVerified)
        {
            throw new ConflictException("User", "email", existingUser.Email.Value);
        }

        if (!_passwordHasher.VerifyPassword(password, existingUser.PasswordHash))
        {
            _logger.LogWarning(
                "Register re-attempt with wrong password for unverified user {Email}",
                existingUser.Email.Value);
            throw new UnauthorizedException("Invalid email or password.");
        }

        existingUser.PasswordHash = _passwordHasher.HashPassword(password);
        existingUser.UpdatedAt = DateTime.UtcNow;

        await _tokenRepository.InvalidateUnusedTokensForUserAsync(existingUser.Id, cancellationToken);

        await CreateTokenAndSendAsync(existingUser, cancellationToken);

        _logger.LogInformation(
            "Unverified user re-registered; verification email resent: {Email} | UserId: {UserId}",
            existingUser.Email.Value,
            existingUser.Id);

        return new RegisterDto
        {
            UserId = existingUser.Id,
            Email = existingUser.Email.Value,
            Message = "Account pending verification updated. Please check your email to verify your account."
        };
    }

    private async Task CreateTokenAndSendAsync(User user, CancellationToken cancellationToken)
    {
        var tokenValue = Guid.NewGuid().ToString("N");
        var verificationToken = new EmailVerificationToken
        {
            UserId = user.Id,
            Token = tokenValue,
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        };

        await _tokenRepository.AddAsync(verificationToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            await _emailService.SendVerificationEmailAsync(user.Email.Value, tokenValue, cancellationToken);
            _logger.LogInformation("Verification email sent to {Email}", user.Email.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send verification email to {Email}. User can resend later.", user.Email.Value);
        }
    }
}
