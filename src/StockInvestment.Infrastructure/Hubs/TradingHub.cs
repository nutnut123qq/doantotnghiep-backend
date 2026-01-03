using Microsoft.AspNetCore.SignalR;

namespace StockInvestment.Infrastructure.Hubs;

/// <summary>
/// SignalR Hub for real-time trading board updates
/// </summary>
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

