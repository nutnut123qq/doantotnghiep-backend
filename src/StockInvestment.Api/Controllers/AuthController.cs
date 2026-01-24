using MediatR;
using Microsoft.AspNetCore.Mvc;
using StockInvestment.Application.Features.Auth.Login;
using StockInvestment.Application.Features.Auth.Register;
using StockInvestment.Application.Features.Auth.VerifyEmail;
using StockInvestment.Application.Features.Auth.ResendVerification;
using StockInvestment.Api.Middleware;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<AuthController> _logger;
    private readonly ICacheService _cacheService;

    public AuthController(
        IMediator mediator, 
        ILogger<AuthController> logger,
        ICacheService cacheService)
    {
        _mediator = mediator;
        _logger = logger;
        _cacheService = cacheService;
    }

    private string GetClientIpAddress()
    {
        // P0-4: Use RemoteIpAddress which is now populated by ForwardedHeadersMiddleware
        // ForwardedHeadersMiddleware validates X-Forwarded-For/X-Real-IP from trusted proxies only
        // and sets Connection.RemoteIpAddress accordingly
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    /// <summary>
    /// Register a new user account
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Login with email and password
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
    {
        try
        {
            var result = await _mediator.Send(command);
            
            // Clear rate limit for this IP on successful login only
            // This allows users who were rate limited to try again immediately after successful login
            try
            {
                var clientIp = GetClientIpAddress();
                var rateLimitKey = $"rate_limit:auth:{clientIp}";
                await _cacheService.RemoveAsync(rateLimitKey);
                _logger.LogDebug("Cleared rate limit for IP {ClientIp} after successful login", clientIp);
            }
            catch (Exception ex)
            {
                // Log but don't fail the login if rate limit clearing fails
                _logger.LogWarning(ex, "Failed to clear rate limit after successful login");
            }
            
            return Ok(result);
        }
        catch (Exception)
        {
            // Re-throw to let GlobalExceptionHandlerMiddleware handle it
            // Don't clear rate limit on failed login attempts
            throw;
        }
    }

    /// <summary>
    /// Verify email address with token
    /// </summary>
    [HttpPost("verify-email")]
    [ProducesResponseType(typeof(VerifyEmailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyEmail([FromQuery] string token)
    {
        var command = new VerifyEmailCommand { Token = token };
        var result = await _mediator.Send(command);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }
        
        return Ok(result);
    }

    /// <summary>
    /// Resend verification email
    /// </summary>
    [HttpPost("resend-verification")]
    [ProducesResponseType(typeof(ResendVerificationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }
}

