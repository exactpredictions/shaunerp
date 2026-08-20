using ShaunERP.Core.Platform;

namespace ShaunERP.Tests;

public class ListQueryTests
{
    [Theory]
    [InlineData(50, 50)]
    [InlineData(100, 100)]
    [InlineData(200, 200)]
    [InlineData(0, 50)]
    [InlineData(75, 50)]
    [InlineData(999, 50)]
    public void NormalizePageSize_only_allows_50_100_200(int input, int expected)
    {
        Assert.Equal(expected, ListQuery.NormalizePageSize(input));
    }

    [Fact]
    public void WithSearch_resets_to_first_page()
    {
        var q = new ListQuery { Page = 4, PageSize = 100 }.WithSearch("acme");
        Assert.Equal(1, q.Page);
        Assert.Equal(100, q.PageSize);
        Assert.Equal("acme", q.Search);
    }
}
