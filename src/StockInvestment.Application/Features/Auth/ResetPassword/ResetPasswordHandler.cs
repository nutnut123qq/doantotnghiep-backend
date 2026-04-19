using MediatR;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Application.Features.Auth.ResetPassword;

/// <summary>
/// Completes the user-facing "forgot password" flow:
///   1. Look up the reset token.
///   2. Validate it (not used, not expired, user active).
///   3. Replace the password hash.
///   4. Mark token as used and invalidate any other outstanding tokens.
///   5. Clear any temporary lockout so the user can sign in immediately.
/// </summary>
public class ResetPasswordHandler : IRequestHandler<ResetPasswordCommand, ResetPasswordDto>
{
    private readonly IPasswordResetTokenRepository _tokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ResetPasswordHandler> _logger;

    public ResetPasswordHandler(
        IPasswordResetTokenRepository tokenRepository,
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        ILogger<ResetPasswordHandler> logger)
    {
        _tokenRepository = tokenRepository;
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ResetPasswordDto> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var token = await _tokenRepository.GetByTokenAsync(request.Token, cancellationToken);

        if (token == null)
        {
            return new ResetPasswordDto
            {
                Success = false,
                Message = "Invalid password reset token."
            };
        }

        if (token.IsUsed)
        {
            return new ResetPasswordDto
            {
                Success = false,
                Message = "This password reset token has already been used."
            };
        }

        if (token.IsExpired)
        {
            return new ResetPasswordDto
            {
                Success = false,
                Message = "This password reset link has expired. Please request a new one."
            };
        }

        var user = token.User ?? await _userRepository.GetByIdAsync(token.UserId, cancellationToken);
        if (user == null || !user.IsActive)
        {
            return new ResetPasswordDto
            {
                Success = false,
                Message = "Account not found or inactive."
            };
        }

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        user.LockoutEnabled = false;
        user.LockoutEnd = null;

        token.IsUsed = true;
        token.UsedAt = DateTime.UtcNow;

        await _tokenRepository.InvalidateUnusedTokensForUserAsync(user.Id, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Password reset successful for user {UserId} ({Email})", user.Id, user.Email.Value);

        return new ResetPasswordDto
        {
            Success = true,
            Message = "Password has been reset successfully. You can now sign in with your new password."
        };
    }
}
