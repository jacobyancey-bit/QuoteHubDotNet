using QuoteHub.Api.Dtos;
using QuoteHub.Api.Services;

namespace QuoteHub.Tests;

public class InMemoryQuoteServiceTests
{
    [Fact]
    public async Task GetAllAsync_ReturnsSeededQuotes()
    {
        var service = new InMemoryQuoteService();

        var page = await service.GetAllAsync();

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(3, page.Items.Count);
        Assert.Equal(1, page.TotalPages);
    }

    [Fact]
    public async Task GetAllAsync_WithCategoryFilter_IsCaseInsensitive()
    {
        var service = new InMemoryQuoteService();

        var page = await service.GetAllAsync("space");

        var single = Assert.Single(page.Items);
        Assert.Equal("Katherine Johnson", single.Author);
    }

    [Fact]
    public async Task GetAllAsync_SupportsPagination()
    {
        var service = new InMemoryQuoteService();

        var page1 = await service.GetAllAsync(page: 1, pageSize: 2);
        var page2 = await service.GetAllAsync(page: 2, pageSize: 2);

        Assert.Equal(3, page1.TotalCount);
        Assert.Equal(2, page1.TotalPages);
        Assert.Equal(2, page1.Items.Count);
        Assert.Single(page2.Items);
    }

    [Fact]
    public async Task CreateAsync_AssignsNextIdAndTrimsInput()
    {
        var service = new InMemoryQuoteService();
        var request = new CreateQuoteRequest("  Failure is not an option.  ", "  Gene Kranz  ", "  Space  ");

        var created = await service.CreateAsync(request);

        Assert.Equal(4, created.Id);
        Assert.Equal("Failure is not an option.", created.Text);
        Assert.Equal("Gene Kranz", created.Author);
        Assert.Equal("Space", created.Category);
        Assert.NotNull(await service.GetByIdAsync(4));
    }

    [Fact]
    public async Task UpdateAsync_WhenQuoteDoesNotExist_ReturnsNull()
    {
        var service = new InMemoryQuoteService();
        var request = new UpdateQuoteRequest("Updated text", "Author", "General");

        var result = await service.UpdateAsync(999, request);

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_HidesQuoteFromSubsequentReads()
    {
        var service = new InMemoryQuoteService();

        var deleted = await service.DeleteAsync(1);
        var fetched = await service.GetByIdAsync(1);
        var remaining = await service.GetAllAsync();

        Assert.True(deleted);
        Assert.Null(fetched);
        Assert.Equal(2, remaining.TotalCount);
        Assert.False(await service.DeleteAsync(1));
    }

    [Fact]
    public async Task SearchAsync_MatchesTextOrAuthor_CaseInsensitively()
    {
        var service = new InMemoryQuoteService();

        var results = await service.SearchAsync("hopper");

        var match = Assert.Single(results);
        Assert.Equal("Grace Hopper", match.Author);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchAsync_WhenQueryIsBlank_ReturnsEmpty(string blankQuery)
    {
        var service = new InMemoryQuoteService();

        var results = await service.SearchAsync(blankQuery);

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_ExcludesDeletedQuotes()
    {
        var service = new InMemoryQuoteService();
        await service.DeleteAsync(2);

        var results = await service.SearchAsync("hopper");

        Assert.Empty(results);
    }
}
