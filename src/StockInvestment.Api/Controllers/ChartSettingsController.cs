using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using System.Security.Claims;
using System.Text.Json;

namespace StockInvestment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChartSettingsController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ChartSettingsController> _logger;

    public ChartSettingsController(
        IUnitOfWork unitOfWork,
        ILogger<ChartSettingsController> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Get chart settings for a symbol
    /// </summary>
    [HttpGet("{symbol}")]
    public async Task<IActionResult> GetSettings(string symbol)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User ID not found in token");
            }

            var settings = await _unitOfWork.ChartSettings.GetByUserAndSymbolAsync(userId, symbol);
            
            if (settings == null)
            {
                return NotFound($"No chart settings found for symbol {symbol}");
            }

            return Ok(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting chart settings for symbol {Symbol}", symbol);
            return StatusCode(500, "An error occurred while retrieving chart settings");
        }
    }

    /// <summary>
    /// Get all chart settings for current user
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllSettings()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User ID not found in token");
            }

            var settings = await _unitOfWork.ChartSettings.GetByUserIdAsync(userId);
            return Ok(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all chart settings");
            return StatusCode(500, "An error occurred while retrieving chart settings");
        }
    }

    /// <summary>
    /// Save or update chart settings
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SaveSettings([FromBody] SaveChartSettingsRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User ID not found in token");
            }

            if (string.IsNullOrWhiteSpace(request.Symbol))
            {
                return BadRequest("Symbol is required");
            }

            // Validate JSON strings
            try
            {
                if (!string.IsNullOrWhiteSpace(request.Indicators))
                {
                    JsonSerializer.Deserialize<string[]>(request.Indicators);
                }
                if (!string.IsNullOrWhiteSpace(request.Drawings))
                {
                    JsonSerializer.Deserialize<object>(request.Drawings);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Invalid JSON in chart settings");
                return BadRequest("Invalid JSON format in Indicators or Drawings");
            }

            var settings = new ChartSettings
            {
                UserId = userId,
                Symbol = request.Symbol.ToUpper(),
                TimeRange = request.TimeRange ?? "3M",
                ChartType = request.ChartType ?? "candlestick",
                Indicators = request.Indicators ?? "[]",
                Drawings = request.Drawings ?? "{}",
            };

            var savedSettings = await _unitOfWork.ChartSettings.SaveSettingsAsync(settings);
            await _unitOfWork.SaveChangesAsync();

            return Ok(savedSettings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving chart settings");
            return StatusCode(500, "An error occurred while saving chart settings");
        }
    }

    /// <summary>
    /// Delete chart settings for a symbol
    /// </summary>
    [HttpDelete("{symbol}")]
    public async Task<IActionResult> DeleteSettings(string symbol)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User ID not found in token");
            }

            var settings = await _unitOfWork.ChartSettings.GetByUserAndSymbolAsync(userId, symbol);
            
            if (settings == null)
            {
                return NotFound($"No chart settings found for symbol {symbol}");
            }

            await _unitOfWork.ChartSettings.DeleteAsync(settings);
            await _unitOfWork.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting chart settings for symbol {Symbol}", symbol);
            return StatusCode(500, "An error occurred while deleting chart settings");
        }
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

public class SaveChartSettingsRequest
{
    public string Symbol { get; set; } = null!;
    public string? TimeRange { get; set; }
    public string? ChartType { get; set; }
    public string? Indicators { get; set; }
    public string? Drawings { get; set; }
}
