using Microsoft.AspNetCore.SignalR;

namespace StockInvestment.Api.Hubs;

public class StockPriceHub : Hub
{
    public async Task JoinTickerGroup(string tickerSymbol)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, tickerSymbol);
    }

    public async Task LeaveTickerGroup(string tickerSymbol)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, tickerSymbol);
    }
}

