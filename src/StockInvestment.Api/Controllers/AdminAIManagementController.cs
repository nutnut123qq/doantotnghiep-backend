using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Api.Controllers;

[ApiController]
[Route("api/admin/ai-management")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminAIManagementController : ControllerBase
{
    private readonly IAIManagementService _aiManagement;
    private readonly ILogger<AdminAIManagementController> _logger;

    public AdminAIManagementController(IAIManagementService aiManagement, ILogger<AdminAIManagementController> logger)
    {
        _aiManagement = aiManagement;
        _logger = logger;
    }

    [HttpGet("providers")]
    public async Task<ActionResult> GetProviders()
    {
        var result = await _aiManagement.GetProvidersAsync();
        return Ok(result);
    }

    [HttpPost("providers/probe")]
    public async Task<ActionResult> ProbeProviders()
    {
        var result = await _aiManagement.ProbeProvidersAsync();
        return Ok(result);
    }

    [HttpGet("pipeline")]
    public async Task<ActionResult> GetPipeline()
    {
        var result = await _aiManagement.GetPipelineAsync();
        return Ok(result);
    }

    [HttpGet("rag/documents")]
    public async Task<ActionResult> GetRagDocuments()
    {
        var result = await _aiManagement.GetRagDocumentsAsync();
        return Ok(result);
    }

    [HttpDelete("rag/documents/{documentId}")]
    public async Task<ActionResult> DeleteRagDocument(string documentId)
    {
        await _aiManagement.DeleteRagDocumentAsync(documentId);
        return Ok(new { documentId, status = "deleted" });
    }

    [HttpGet("cache/stats")]
    public async Task<ActionResult> GetCacheStats()
    {
        var result = await _aiManagement.GetCacheStatsAsync();
        return Ok(result);
    }

    [HttpPut("cache/ttl")]
    public async Task<ActionResult> UpdateCacheTtl([FromBody] AiCacheTtlUpdateDto dto)
    {
        await _aiManagement.UpdateCacheTtlAsync(dto);
        return Ok(new { status = "updated" });
    }

    [HttpGet("jobs")]
    public async Task<ActionResult> GetJobs()
    {
        var result = await _aiManagement.GetJobsAsync();
        return Ok(result);
    }

    [HttpPost("jobs/{jobId}/retry")]
    public async Task<ActionResult> RetryJob(string jobId)
    {
        await _aiManagement.RetryJobAsync(jobId);
        return Ok(new { jobId, status = "requeued" });
    }

    [HttpDelete("jobs/{jobId}")]
    public async Task<ActionResult> CancelJob(string jobId)
    {
        await _aiManagement.CancelJobAsync(jobId);
        return Ok(new { jobId, status = "cancelled" });
    }

    [HttpGet("parameters")]
    public async Task<ActionResult> GetParameters()
    {
        var result = await _aiManagement.GetParametersAsync();
        return Ok(result);
    }

    [HttpPut("parameters")]
    public async Task<ActionResult> UpdateParameters([FromBody] AiParametersUpdateDto dto)
    {
        await _aiManagement.UpdateParametersAsync(dto);
        return Ok(new { status = "updated" });
    }

    [HttpGet("traces")]
    public async Task<ActionResult> GetTraces([FromQuery] int limit = 20)
    {
        var result = await _aiManagement.GetTracesAsync(limit);
        return Ok(result);
    }

    [HttpDelete("traces")]
    public async Task<ActionResult> ClearTraces()
    {
        await _aiManagement.ClearTracesAsync();
        return Ok(new { status = "cleared" });
    }
}
