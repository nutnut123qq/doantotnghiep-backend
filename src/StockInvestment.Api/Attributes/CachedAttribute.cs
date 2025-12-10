using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using StockInvestment.Application.Interfaces;
using System.Text;

namespace StockInvestment.Api.Attributes;

/// <summary>
/// Attribute for caching API responses
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class CachedAttribute : Attribute, IAsyncActionFilter
{
    private readonly int _timeToLiveSeconds;

    public CachedAttribute(int timeToLiveSeconds = 60)
    {
        _timeToLiveSeconds = timeToLiveSeconds;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var cacheService = context.HttpContext.RequestServices.GetService<ICacheService>();
        
        if (cacheService == null)
        {
            await next();
            return;
        }

        var cacheKey = GenerateCacheKeyFromRequest(context.HttpContext.Request);
        var cachedResponse = await cacheService.GetAsync<object>(cacheKey);

        if (cachedResponse != null)
        {
            context.Result = new OkObjectResult(cachedResponse);
            return;
        }

        var executedContext = await next();

        if (executedContext.Result is OkObjectResult okObjectResult)
        {
            await cacheService.SetAsync(
                cacheKey,
                okObjectResult.Value!,
                TimeSpan.FromSeconds(_timeToLiveSeconds));
        }
    }

    private static string GenerateCacheKeyFromRequest(HttpRequest request)
    {
        var keyBuilder = new StringBuilder();
        keyBuilder.Append($"{request.Path}");

        foreach (var (key, value) in request.Query.OrderBy(x => x.Key))
        {
            keyBuilder.Append($"|{key}-{value}");
        }

        return keyBuilder.ToString();
    }
}

