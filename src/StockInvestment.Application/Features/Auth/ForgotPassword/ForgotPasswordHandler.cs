using MediatR;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using StockInvestment.Domain.ValueObjects;

namespace StockInvestment.Application.Features.Auth.ForgotPassword;

/// <summary>
/// Starts the user-facing "forgot password" flow:
///   1. Validate email shape (handled by validator).
///   2. If the account exists, invalidate old tokens and issue a fresh one.
///   3. Send a reset link via email.
///   4. ALWAYS return a generic success message to prevent user enumeration.
/// </summary>
public class ForgotPasswordHandler : IRequestHandler<ForgotPasswordCommand, ForgotPasswordDto>
{
    private const string GenericMessage =
        "If an account exists with this email, a password reset link has been sent. Please check your inbox.";

    private readonly IUserRepository _userRepository;
    private readonly IPasswordResetTokenRepository _tokenRepository;
    private readonly IEmailService _emailService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ForgotPasswordHandler> _logger;

    public ForgotPasswordHandler(
        IUserRepository userRepository,
        IPasswordResetTokenRepository tokenRepository,
        IEmailService emailService,
        IUnitOfWork unitOfWork,
        ILogger<ForgotPasswordHandler> logger)
    {
        _userRepository = userRepository;
        _tokenRepository = tokenRepository;
        _emailService = emailService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ForgotPasswordDto> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var email = Email.Create(request.Email);
        var user = await _userRepository.GetByEmailAsync(email, cancellationToken);

        if (user == null)
        {
            _logger.LogInformation("Forgot-password requested for unknown email (no email sent).");
            return new ForgotPasswordDto { Success = true, Message = GenericMessage };
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Forgot-password requested for inactive user {UserId}", user.Id);
            return new ForgotPasswordDto { Success = true, Message = GenericMessage };
        }

        await _tokenRepository.InvalidateUnusedTokensForUserAsync(user.Id, cancellationToken);

        var tokenValue = Guid.NewGuid().ToString("N");
        var resetToken = new PasswordResetToken
        {
            UserId = user.Id,
            Token = tokenValue
        };

        await _tokenRepository.AddAsync(resetToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            await _emailService.SendPasswordResetEmailAsync(user.Email.Value, tokenValue, cancellationToken);
            _logger.LogInformation("Password reset email sent to {Email}", user.Email.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email to {Email}", user.Email.Value);
        }

        return new ForgotPasswordDto { Success = true, Message = GenericMessage };
    }
}
