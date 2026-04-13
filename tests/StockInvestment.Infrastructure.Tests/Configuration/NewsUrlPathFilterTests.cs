using StockInvestment.Infrastructure.Configuration;
using Xunit;

namespace StockInvestment.Infrastructure.Tests.Configuration;

public class NewsUrlPathFilterTests
{
    [Fact]
    public void IsAllowed_rejects_url_with_blocked_segment()
    {
        var blocked = new[] { "phap-luat" };
        Assert.False(NewsUrlPathFilter.IsAllowed("https://tuoitre.vn/phap-luat/foo-bar.htm", blocked));
    }

    [Fact]
    public void IsAllowed_accepts_chung_khoan_path_when_blocked_list_excludes_it()
    {
        var blocked = new[] { "phap-luat", "giai-tri" };
        Assert.True(NewsUrlPathFilter.IsAllowed("https://tuoitre.vn/chung-khoan/bai-viet.htm", blocked));
    }

    [Fact]
    public void IsAllowed_empty_blocked_list_allows_valid_url()
    {
        Assert.True(NewsUrlPathFilter.IsAllowed("https://tuoitre.vn/phap-luat/foo.htm", Array.Empty<string>()));
    }

    [Fact]
    public void IsAllowed_null_blocked_list_allows_valid_url()
    {
        Assert.True(NewsUrlPathFilter.IsAllowed("https://tuoitre.vn/phap-luat/foo.htm", null));
    }

    [Fact]
    public void IsAllowed_rejects_null_or_whitespace_url()
    {
        Assert.False(NewsUrlPathFilter.IsAllowed(null, new[] { "phap-luat" }));
        Assert.False(NewsUrlPathFilter.IsAllowed("   ", new[] { "phap-luat" }));
    }

    [Fact]
    public void IsAllowed_rejects_unparseable_url()
    {
        Assert.False(NewsUrlPathFilter.IsAllowed("not-a-url", new[] { "phap-luat" }));
    }

    [Fact]
    public void IsAllowed_matching_is_case_insensitive_for_segments()
    {
        var blocked = new[] { "Phap-Luat" };
        Assert.False(NewsUrlPathFilter.IsAllowed("https://tuoitre.vn/phap-luat/x.htm", blocked));
    }
}
