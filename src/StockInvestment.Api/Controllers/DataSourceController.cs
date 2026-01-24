using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockInvestment.Api.Attributes;
using StockInvestment.Application.Features.Admin.DataSources.GetDataSources;
using StockInvestment.Application.Features.Admin.DataSources.CreateDataSource;
using StockInvestment.Application.Features.Admin.DataSources.UpdateDataSource;
using StockInvestment.Application.Features.Admin.DataSources.DeleteDataSource;
using StockInvestment.Application.Features.Admin.DataSources.TestConnection;
using StockInvestment.Domain.Enums;

namespace StockInvestment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[AdminOnly]
public class DataSourceController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<DataSourceController> _logger;

    public DataSourceController(IMediator mediator, ILogger<DataSourceController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Get all data sources (Admin only)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<GetDataSourcesResponse>> GetAll([FromQuery] DataSourceType? type)
    {
        // P1-2: Let GlobalExceptionHandlerMiddleware handle exceptions - no need for try/catch here
        var query = new GetDataSourcesQuery { Type = type };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Get data source by ID (Admin only)
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<StockInvestment.Application.Features.Admin.DataSources.GetDataSources.DataSourceDto>> GetById(Guid id)
    {
        // P1-2: Let GlobalExceptionHandlerMiddleware handle exceptions
        var query = new GetDataSourcesQuery();
        var result = await _mediator.Send(query);
        var dataSource = result.DataSources.FirstOrDefault(ds => ds.Id == id);
        
        if (dataSource == null)
        {
            return NotFound();
        }
        
        return Ok(dataSource);
    }

    /// <summary>
    /// Create a new data source (Admin only)
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<StockInvestment.Application.Features.Admin.DataSources.CreateDataSource.DataSourceDto>> Create([FromBody] CreateDataSourceCommand command)
    {
        // P1-2: Let GlobalExceptionHandlerMiddleware handle exceptions
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Update a data source (Admin only)
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<StockInvestment.Application.Features.Admin.DataSources.UpdateDataSource.DataSourceDto>> Update(Guid id, [FromBody] UpdateDataSourceCommand command)
    {
        // P1-2: Let GlobalExceptionHandlerMiddleware handle exceptions (InvalidOperationException -> 400 BadRequest)
        command.Id = id;
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Delete a data source (Admin only)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        // P1-2: Let GlobalExceptionHandlerMiddleware handle exceptions
        var command = new DeleteDataSourceCommand { Id = id };
        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>
    /// Test connection to a data source (Admin only)
    /// </summary>
    [HttpPost("{id}/test")]
    public async Task<ActionResult<TestConnectionResponse>> TestConnection(Guid id)
    {
        // P1-2: Let GlobalExceptionHandlerMiddleware handle exceptions
        // InvalidOperationException from SSRF validation will be handled as 400 BadRequest
        var command = new TestConnectionCommand { Id = id };
        var result = await _mediator.Send(command);
        return Ok(result);
    }
}

