using StockInvestment.Domain.Entities;
using StockInvestment.Infrastructure.External;
using Xunit;

namespace StockInvestment.Infrastructure.Tests.External;

public class CorporateEventTextHelperTests
{
    [Fact]
    public void TryResolveTickerId_prefers_first_known_symbol()
    {
        var id = Guid.NewGuid();
        var map = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase)
        {
            ["AAA"] = Guid.NewGuid(),
            ["FPT"] = id,
        };

        Assert.True(CorporateEventTextHelper.TryResolveTickerId("Tin về FPT và AAA", map, out var resolved));
        Assert.Equal(id, resolved);
    }

    [Fact]
    public void DetermineEventType_detects_agm()
    {
        var t = CorporateEventTextHelper.DetermineEventType("ĐHĐCĐ thường niên 2024");
        Assert.Equal(CorporateEventType.AGM, t);
    }
}
