using StockInvestment.Infrastructure.Configuration;
using Xunit;

namespace StockInvestment.Infrastructure.Tests.Configuration;

public class IngestionFallbackOptionsTests
{
    [Fact]
    public void NewsSourceConfig_has_fallback_defaults()
    {
        var source = new NewsSourceConfig();

        Assert.True(source.Enabled);
        Assert.Equal(100, source.Priority);
        Assert.Empty(source.FallbackSources);
        Assert.Null(source.MinItemsBeforeFallback);
    }

    [Fact]
    public void NewsSourceConfig_supports_nested_fallback_chain()
    {
        var source = new NewsSourceConfig
        {
            Name = "Primary",
            Kind = "Rss",
            FallbackSources =
            [
                new NewsSourceConfig
                {
                    Name = "Fallback1",
                    Kind = "HtmlBuiltin",
                    HtmlTemplate = "VnExpress",
                    FallbackSources =
                    [
                        new NewsSourceConfig
                        {
                            Name = "Fallback2",
                            Kind = "HtmlGeneric",
                            Url = "https://example.com/chung-khoan"
                        }
                    ]
                }
            ]
        };

        Assert.Single(source.FallbackSources);
        Assert.Single(source.FallbackSources[0].FallbackSources);
        Assert.Equal("Fallback2", source.FallbackSources[0].FallbackSources[0].Name);
    }

    [Fact]
    public void FinancialAndEventOptions_expose_fallback_thresholds()
    {
        var financial = new FinancialIngestionOptions();
        var eventIngestion = new EventIngestionOptions();

        Assert.True(financial.MinReportsBeforeFallback >= 0);
        Assert.NotNull(financial.Sources);
        Assert.True(eventIngestion.MinItemsBeforeFallback >= 0);
        Assert.NotNull(eventIngestion.Sources);
    }
}
