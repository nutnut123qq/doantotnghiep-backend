using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace StockInvestment.Api.Controllers;

[ApiController]
[Route("api/layouts")]
[Authorize]
public class SharedLayoutsController : ControllerBase
{
    private const int MaxLayoutJsonBytes = 200 * 1024;
    private static readonly TimeSpan MinExpiry = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan MaxExpiry = TimeSpan.FromDays(90);
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromDays(30);
    private const int CodeLength = 12;
    private const int MaxCodeRetries = 5;
    private const string CodeChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SharedLayoutsController> _logger;

    public SharedLayoutsController(
        IUnitOfWork unitOfWork,
        ILogger<SharedLayoutsController> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Share layout by generating a share code
    /// </summary>
    [HttpPost("share")]
    public async Task<IActionResult> ShareLayout([FromBody] ShareLayoutRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User ID not found in token");
            }

            if (string.IsNullOrWhiteSpace(request.LayoutJson))
            {
                return BadRequest("Layout JSON is required");
            }

            if (Encoding.UTF8.GetByteCount(request.LayoutJson) > MaxLayoutJsonBytes)
            {
                return BadRequest("Layout JSON exceeds 200KB limit");
            }

            if (!IsValidJson(request.LayoutJson))
            {
                return BadRequest("Layout JSON is invalid");
            }

            var now = DateTime.UtcNow;
            var expiresAt = request.ExpiresAt ?? now.Add(DefaultExpiry);
            var minExpiryAt = now.Add(MinExpiry);
            var maxExpiryAt = now.Add(MaxExpiry);

            if (expiresAt < minExpiryAt || expiresAt > maxExpiryAt)
            {
                return BadRequest("ExpiresAt must be between 10 minutes and 90 days from now");
            }

            var code = await GenerateUniqueCodeAsync();

            var sharedLayout = new SharedLayout
            {
                OwnerId = userId,
                Code = code,
                LayoutJson = request.LayoutJson,
                IsPublic = request.IsPublic ?? false,
                CreatedAt = now,
                ExpiresAt = expiresAt
            };

            await _unitOfWork.Repository<SharedLayout>().AddAsync(sharedLayout);
            await _unitOfWork.SaveChangesAsync();

            return Ok(new
            {
                code = sharedLayout.Code,
                expiresAt = sharedLayout.ExpiresAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sharing layout");
            return StatusCode(500, "An error occurred while sharing layout");
        }
    }

    /// <summary>
    /// Get shared layout by code
    /// </summary>
    [HttpGet("shared/{code}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSharedLayoutByCode(string code)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return NotFound();
            }

            var sharedLayout = await _unitOfWork.Repository<SharedLayout>()
                .FirstOrDefaultAsync(sl => sl.Code == code);

            if (sharedLayout == null || sharedLayout.ExpiresAt <= DateTime.UtcNow)
            {
                return NotFound();
            }

            return Ok(new
            {
                layoutJson = sharedLayout.LayoutJson,
                createdAt = sharedLayout.CreatedAt,
                expiresAt = sharedLayout.ExpiresAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting shared layout by code");
            return StatusCode(500, "An error occurred while retrieving shared layout");
        }
    }

    /// <summary>
    /// Get current user's shared layouts
    /// </summary>
    [HttpGet("shared")]
    public async Task<IActionResult> GetMySharedLayouts()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User ID not found in token");
            }

            var layouts = await _unitOfWork.Repository<SharedLayout>()
                .FindAsync(sl => sl.OwnerId == userId);

            var response = layouts
                .OrderByDescending(sl => sl.CreatedAt)
                .Select(sl => new
                {
                    id = sl.Id,
                    code = sl.Code,
                    createdAt = sl.CreatedAt,
                    expiresAt = sl.ExpiresAt,
                    isPublic = sl.IsPublic
                });

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting shared layouts for user");
            return StatusCode(500, "An error occurred while retrieving shared layouts");
        }
    }

    private static bool IsValidJson(string json)
    {
        try
        {
            using var _ = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task<string> GenerateUniqueCodeAsync()
    {
        var repo = _unitOfWork.Repository<SharedLayout>();
        for (var attempt = 0; attempt < MaxCodeRetries; attempt++)
        {
            var code = GenerateCode(CodeLength);
            var exists = await repo.AnyAsync(sl => sl.Code == code);
            if (!exists)
            {
                return code;
            }
        }

        throw new InvalidOperationException("Unable to generate unique share code");
    }

    private static string GenerateCode(int length)
    {
        var bytes = new byte[length];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(bytes);

        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = CodeChars[bytes[i] % CodeChars.Length];
        }

        return new string(chars);
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }
        return Guid.Empty;
    }
}

public class ShareLayoutRequest
{
    public string LayoutJson { get; set; } = string.Empty;
    public bool? IsPublic { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
