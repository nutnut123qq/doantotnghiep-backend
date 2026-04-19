using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StockInvestment.Application.Features.Auth.Register;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;
using StockInvestment.Domain.Exceptions;
using StockInvestment.Domain.ValueObjects;
using Xunit;

namespace StockInvestment.Infrastructure.Tests.Features.Auth;

public class RegisterHandlerTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<IEmailVerificationTokenRepository> _tokenRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private RegisterHandler CreateHandler() => new(
        _userRepo.Object,
        _hasher.Object,
        _emailService.Object,
        _tokenRepo.Object,
        _uow.Object,
        NullLogger<RegisterHandler>.Instance);

    private static RegisterCommand Command(string email = "user@example.com", string password = "Test123!@#") => new()
    {
        Email = email,
        Password = password,
        ConfirmPassword = password
    };

    [Fact]
    public async Task Handle_NewUser_CreatesUserAndSendsVerificationEmail()
    {
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        _hasher.Setup(h => h.HashPassword(It.IsAny<string>())).Returns("HASH");

        var cmd = Command();
        var result = await CreateHandler().Handle(cmd, CancellationToken.None);

        Assert.Equal(cmd.Email, result.Email);
        _userRepo.Verify(r => r.AddAsync(It.Is<User>(u => !u.IsEmailVerified && u.PasswordHash == "HASH"), It.IsAny<CancellationToken>()), Times.Once);
        _tokenRepo.Verify(r => r.AddAsync(It.IsAny<EmailVerificationToken>(), It.IsAny<CancellationToken>()), Times.Once);
        _tokenRepo.Verify(r => r.InvalidateUnusedTokensForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _emailService.Verify(e => e.SendVerificationEmailAsync(cmd.Email, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingVerifiedUser_ThrowsConflict()
    {
        var cmd = Command();
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Email = Email.Create(cmd.Email),
                PasswordHash = "HASH",
                Role = UserRole.Investor,
                IsEmailVerified = true,
                IsActive = true
            });

        await Assert.ThrowsAsync<ConflictException>(() => CreateHandler().Handle(cmd, CancellationToken.None));
        _userRepo.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _emailService.Verify(e => e.SendVerificationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ExistingUnverifiedUser_WrongPassword_ThrowsUnauthorized()
    {
        var cmd = Command();
        var existing = new User
        {
            Email = Email.Create(cmd.Email),
            PasswordHash = "OLD_HASH",
            Role = UserRole.Investor,
            IsEmailVerified = false,
            IsActive = true
        };
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _hasher.Setup(h => h.VerifyPassword(cmd.Password, "OLD_HASH")).Returns(false);

        await Assert.ThrowsAsync<UnauthorizedException>(() => CreateHandler().Handle(cmd, CancellationToken.None));
        _tokenRepo.Verify(r => r.InvalidateUnusedTokensForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _emailService.Verify(e => e.SendVerificationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ExistingUnverifiedUser_CorrectPassword_UpsertsTokenAndSendsEmail()
    {
        var cmd = Command();
        var existing = new User
        {
            Id = Guid.NewGuid(),
            Email = Email.Create(cmd.Email),
            PasswordHash = "OLD_HASH",
            Role = UserRole.Investor,
            IsEmailVerified = false,
            IsActive = true
        };
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _hasher.Setup(h => h.VerifyPassword(cmd.Password, "OLD_HASH")).Returns(true);
        _hasher.Setup(h => h.HashPassword(cmd.Password)).Returns("NEW_HASH");

        var result = await CreateHandler().Handle(cmd, CancellationToken.None);

        Assert.Equal(existing.Id, result.UserId);
        Assert.Equal(cmd.Email, result.Email);
        Assert.Equal("NEW_HASH", existing.PasswordHash);
        _userRepo.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _tokenRepo.Verify(r => r.InvalidateUnusedTokensForUserAsync(existing.Id, It.IsAny<CancellationToken>()), Times.Once);
        _tokenRepo.Verify(r => r.AddAsync(It.Is<EmailVerificationToken>(t => t.UserId == existing.Id && !t.IsUsed), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _emailService.Verify(e => e.SendVerificationEmailAsync(cmd.Email, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
