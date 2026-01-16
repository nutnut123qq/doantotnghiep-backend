using MediatR;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.ValueObjects;

namespace StockInvestment.Application.Features.Auth.ResendVerification;

public class ResendVerificationHandler : IRequestHandler<ResendVerificationCommand, ResendVerificationDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IEmailVerificationTokenRepository _tokenRepository;
    private readonly IEmailService _emailService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ResendVerificationHandler> _logger;

    public ResendVerificationHandler(
        IUserRepository userRepository,
        IEmailVerificationTokenRepository tokenRepository,
        IEmailService emailService,
        IUnitOfWork unitOfWork,
        ILogger<ResendVerificationHandler> logger)
    {
        _userRepository = userRepository;
        _tokenRepository = tokenRepository;
        _emailService = emailService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ResendVerificationDto> Handle(ResendVerificationCommand request, CancellationToken cancellationToken)
    {
        var email = Email.Create(request.Email);
        var user = await _userRepository.GetByEmailAsync(email, cancellationToken);

        if (user == null)
        {
            // Don't reveal if user exists or not for security
            return new ResendVerificationDto
            {
                Success = true,
                Message = "If an account exists with this email, a verification email has been sent."
            };
        }

        if (user.IsEmailVerified)
        {
            return new ResendVerificationDto
            {
                Success = false,
                Message = "This email is already verified."
            };
        }

        // Invalidate old tokens
        var oldToken = await _tokenRepository.GetActiveTokenByUserIdAsync(user.Id, cancellationToken);
        if (oldToken != null)
        {
            oldToken.IsUsed = true;
        }

        // Generate new token
        var token = Guid.NewGuid().ToString("N");
        var verificationToken = new Domain.Entities.EmailVerificationToken
        {
            UserId = user.Id,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        };

        await _tokenRepository.AddAsync(verificationToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Send verification email
        try
        {
            await _emailService.SendVerificationEmailAsync(email.Value, token, cancellationToken);
            _logger.LogInformation("Verification email resent to {Email}", email.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send verification email to {Email}", email.Value);
            return new ResendVerificationDto
            {
                Success = false,
                Message = "Failed to send verification email. Please try again later."
            };
        }

        return new ResendVerificationDto
        {
            Success = true,
            Message = "Verification email has been sent. Please check your inbox."
        };
    }
}
