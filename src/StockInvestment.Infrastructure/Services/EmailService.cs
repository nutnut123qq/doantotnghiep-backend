using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Infrastructure.Services;

/// <summary>
/// SMTP-based email service (MailKit) — reliable STARTTLS with providers like Brevo.
/// </summary>
public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly string _smtpServer;
    private readonly int _smtpPort;
    private readonly string _senderEmail;
    private readonly string _senderName;
    private readonly string _smtpUsername;
    private readonly string _smtpPassword;
    private readonly bool _enableSsl;
    private readonly string _baseUrl;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _logger = logger;
        _smtpServer = configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
        _smtpPort = int.Parse(configuration["EmailSettings:SmtpPort"] ?? "587");
        _senderEmail = configuration["EmailSettings:SenderEmail"] ?? "noreply@stockinvestment.com";
        _senderName = configuration["EmailSettings:SenderName"] ?? "Stock Investment Platform";
        _smtpUsername = configuration["EmailSettings:Username"] ?? string.Empty;
        _smtpPassword = configuration["EmailSettings:Password"] ?? string.Empty;
        _enableSsl = bool.Parse(configuration["EmailSettings:EnableSsl"] ?? "true");
        _baseUrl = configuration["EmailSettings:BaseUrl"] ?? "http://localhost:3000";
    }

    public async Task SendVerificationEmailAsync(string email, string verificationToken, CancellationToken cancellationToken = default)
    {
        try
        {
            var verificationLink = $"{_baseUrl}/verify-email?token={verificationToken}";

            var subject = "Verify Your Email Address";
            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .button {{ display: inline-block; padding: 12px 24px; background-color: #007bff; color: white; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
        .button:hover {{ background-color: #0056b3; }}
        .footer {{ margin-top: 30px; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <h2>Welcome to Stock Investment Platform</h2>
        <p>Thank you for registering! Please verify your email address by clicking the button below:</p>
        <a href='{verificationLink}' class='button'>Verify Email Address</a>
        <p>Or copy and paste this link into your browser:</p>
        <p>{verificationLink}</p>
        <p>This link will expire in 24 hours.</p>
        <div class='footer'>
            <p>If you did not create an account, please ignore this email.</p>
        </div>
    </div>
</body>
</html>";

            await SendEmailAsync(email, subject, body, cancellationToken);
            _logger.LogInformation("Verification email sent to {Email}", email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending verification email to {Email}", email);
            throw;
        }
    }

    public async Task SendPasswordResetEmailAsync(string email, string resetToken, CancellationToken cancellationToken = default)
    {
        try
        {
            var resetLink = $"{_baseUrl}/reset-password?token={resetToken}";

            var subject = "Reset Your Password";
            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .button {{ display: inline-block; padding: 12px 24px; background-color: #dc3545; color: white; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
        .footer {{ margin-top: 30px; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <h2>Password Reset Request</h2>
        <p>You requested to reset your password. Click the button below to reset it:</p>
        <a href='{resetLink}' class='button'>Reset Your Password</a>
        <p>Or copy and paste this link into your browser:</p>
        <p>{resetLink}</p>
        <p>This link will expire in 30 minutes.</p>
        <div class='footer'>
            <p>If you did not request a password reset, please ignore this email.</p>
        </div>
    </div>
</body>
</html>";

            await SendEmailAsync(email, subject, body, cancellationToken);
            _logger.LogInformation("Password reset email sent to {Email}", email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending password reset email to {Email}", email);
            throw;
        }
    }

    private async Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_smtpUsername) || string.IsNullOrWhiteSpace(_smtpPassword))
        {
            throw new InvalidOperationException(
                "Email is not configured: set EmailSettings:Username and EmailSettings:Password (e.g. in appsettings.Development.json or user secrets).");
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_senderName, _senderEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = body };

        using var client = new SmtpClient();

        var secure =
            _smtpPort == 465
                ? SecureSocketOptions.SslOnConnect
                : _enableSsl
                    ? SecureSocketOptions.StartTls
                    : SecureSocketOptions.None;

        await client.ConnectAsync(_smtpServer, _smtpPort, secure, cancellationToken);
        await client.AuthenticateAsync(_smtpUsername, _smtpPassword, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
