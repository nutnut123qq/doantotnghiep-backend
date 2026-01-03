using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockInvestment.Api.Attributes;
using StockInvestment.Application.Features.Admin.AIModelConfig.GetAIModelConfig;
using StockInvestment.Application.Features.Admin.AIModelConfig.UpdateAIModelConfig;
using StockInvestment.Application.Features.Admin.AIModelConfig.GetPerformance;

namespace StockInvestment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[AdminOnly]
public class AIModelConfigController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<AIModelConfigController> _logger;

    public AIModelConfigController(IMediator mediator, ILogger<AIModelConfigController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Get AI model configuration (Admin only)
    /// </summary>
    [HttpGet("config")]
    public async Task<ActionResult<StockInvestment.Application.Features.Admin.AIModelConfig.GetAIModelConfig.AIModelConfigDto>> GetConfig()
    {
        try
        {
            var query = new GetAIModelConfigQuery();
            var result = await _mediator.Send(query);
            
            if (result == null)
            {
                return NotFound("AI model configuration not found");
            }
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting AI model config");
            return StatusCode(500, "An error occurred while fetching AI model config");
        }
    }

    /// <summary>
    /// Update AI model configuration (Admin only)
    /// </summary>
    [HttpPut("config")]
    public async Task<ActionResult<StockInvestment.Application.Features.Admin.AIModelConfig.UpdateAIModelConfig.AIModelConfigDto>> UpdateConfig([FromBody] UpdateAIModelConfigCommand command)
    {
        try
        {
            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating AI model config");
            return StatusCode(500, "An error occurred while updating AI model config");
        }
    }

    /// <summary>
    /// Get AI model performance metrics (Admin only)
    /// </summary>
    [HttpGet("performance")]
    public async Task<ActionResult<GetAIModelPerformanceResponse>> GetPerformance([FromQuery] DateTime? startDate)
    {
        try
        {
            var query = new GetAIModelPerformanceQuery { StartDate = startDate };
            var result = await _mediator.Send(query);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting AI model performance");
            return StatusCode(500, "An error occurred while fetching performance metrics");
        }
    }
}

