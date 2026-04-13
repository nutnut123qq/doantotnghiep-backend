using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StockInvestment.Api.Configuration;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Api.Controllers;

[ApiController]
[Route("api")]
[AllowAnonymous]
public class AnalystContextController : ControllerBase
{
    private readonly IAnalystContextService _analystContext;
    private readonly AnalystContextOptions _options;
    private readonly ILogger<AnalystContextController> _logger;

    public AnalystContextController(
        IAnalystContextService analystContext,
        IOptions<AnalystContextOptions> options,
        ILogger<AnalystContextController> logger)
    {
        _analystContext = analystContext;
        _options = options.Value;
        _logger = logger;
    }

    private bool TryValidateInternalKey()
    {
        var configured = _options.ApiKey;
        if (string.IsNullOrWhiteSpace(configured))
        {
            _logger.LogDebug(
                "AnalystContext:ApiKey is not set; allowing unauthenticated access to analyst context endpoints.");
            return true;
        }

        if (!Request.Headers.TryGetValue("X-Internal-Api-Key", out var provided))
            return false;

        return string.Equals(provided.ToString(), configured, StringComparison.Ordinal);
    }

    /// <summary>
    /// Plain-text recent news for a symbol (for external AI analyst services).
    /// </summary>
    [HttpGet("rag/news-context")]
    public async Task<IActionResult> GetNewsContext(
        [FromQuery] string symbol,
        [FromQuery] int topK = 8,
        [FromQuery] int days = 7,
        CancellationToken cancellationToken = default)
    {
        if (!TryValidateInternalKey())
            return Unauthorized(new { error = "Invalid or missing X-Internal-Api-Key" });

        if (string.IsNullOrWhiteSpace(symbol))
            return BadRequest(new { error = "symbol is required" });

        var text = await _analystContext.BuildNewsContextAsync(symbol, topK, days, cancellationToken);
        return Ok(new { news_context = text });
    }

    /// <summary>
    /// Plain-text technical summary (OHLCV window + indicators) for a symbol.
    /// </summary>
    [HttpGet("market/{symbol}/tech-summary")]
    public async Task<IActionResult> GetTechSummary(
        string symbol,
        [FromQuery] int limit = 48,
        CancellationToken cancellationToken = default)
    {
        if (!TryValidateInternalKey())
            return Unauthorized(new { error = "Invalid or missing X-Internal-Api-Key" });

        if (string.IsNullOrWhiteSpace(symbol))
            return BadRequest(new { error = "symbol is required" });

        var text = await _analystContext.BuildTechContextAsync(symbol, limit, cancellationToken);
        return Ok(new { tech_context = text });
    }
}
