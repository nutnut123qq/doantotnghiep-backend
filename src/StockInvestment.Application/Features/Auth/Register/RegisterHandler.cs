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
    private readonly ILogger<RegisterHandler> _logger;

    public RegisterHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ILogger<RegisterHandler> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<RegisterDto> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var email = Email.Create(request.Email);

        // Check if user already exists
        var existingUser = await _userRepository.GetByEmailAsync(email, cancellationToken);

        if (existingUser != null)
        {
            throw new ConflictException("User", "email", email.Value);
        }

        // Create new user with BCrypt hashed password
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
}

