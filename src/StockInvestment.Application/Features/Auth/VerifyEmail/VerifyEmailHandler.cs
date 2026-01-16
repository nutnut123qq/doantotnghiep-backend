using MediatR;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Exceptions;

namespace StockInvestment.Application.Features.Auth.VerifyEmail;

public class VerifyEmailHandler : IRequestHandler<VerifyEmailCommand, VerifyEmailDto>
{
    private readonly IEmailVerificationTokenRepository _tokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<VerifyEmailHandler> _logger;

    public VerifyEmailHandler(
        IEmailVerificationTokenRepository tokenRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ILogger<VerifyEmailHandler> logger)
    {
        _tokenRepository = tokenRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
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

        // Verify the user
        var user = await _userRepository.GetByIdAsync(token.UserId, cancellationToken);
        if (user == null)
        {
            return new VerifyEmailDto
            {
                Success = false,
                Message = "User not found."
            };
        }

        // Mark token as used
        token.IsUsed = true;
        token.UsedAt = DateTime.UtcNow;

        // Mark user as verified
        user.IsEmailVerified = true;
        user.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Email verified for user {UserId} ({Email})", user.Id, user.Email.Value);

        return new VerifyEmailDto
        {
            Success = true,
            Message = "Email verified successfully. You can now log in."
        };
    }
}
