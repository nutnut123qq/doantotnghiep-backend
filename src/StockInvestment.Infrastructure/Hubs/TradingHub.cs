using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace StockInvestment.Infrastructure.Hubs;

/// <summary>
/// SignalR Hub for real-time trading board updates
/// P2-1: Requires authentication for realtime updates
/// </summary>
[Authorize] // P2-1: Require authentication for realtime trading updates
public class TradingHub : Hub
{
    public async Task JoinTickerGroup(string tickerSymbol)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, tickerSymbol);
    }

    public async Task LeaveTickerGroup(string tickerSymbol)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, tickerSymbol);
    }

    public async Task SubscribeToMarket(string exchange)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"market_{exchange}");
    }

    public async Task UnsubscribeFromMarket(string exchange)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"market_{exchange}");
    }
}

