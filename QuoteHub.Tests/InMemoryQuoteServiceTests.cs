using QuoteHub.Api.Dtos;
using QuoteHub.Api.Services;

namespace QuoteHub.Tests;

public class InMemoryQuoteServiceTests
{
    [Fact]
    public async Task GetAllAsync_ReturnsSeededQuotes()
    {
        // Arrange
        var service = new InMemoryQuoteService();

        // Act
        var quotes = await service.GetAllAsync();

        // Assert
        Assert.Equal(3, quotes.Count);
    }

    [Fact]
    public async Task GetAllAsync_WithCategoryFilter_IsCaseInsensitive()
    {
        // Arrange
        var service = new InMemoryQuoteService();

        // Act
        var spaceQuotes = await service.GetAllAsync("space");

        // Assert
        var single = Assert.Single(spaceQuotes);
        Assert.Equal("Katherine Johnson", single.Author);
    }

    [Fact]
    public async Task CreateAsync_AssignsNextIdAndTrimsInput()
    {
        // Arrange
        var service = new InMemoryQuoteService();
        var request = new CreateQuoteRequest("  Failure is not an option.  ", "  Gene Kranz  ", "  Space  ");

        // Act
        var created = await service.CreateAsync(request);

        // Assert
        Assert.Equal(4, created.Id);
        Assert.Equal("Failure is not an option.", created.Text);
        Assert.Equal("Gene Kranz", created.Author);
        Assert.Equal("Space", created.Category);
        Assert.NotNull(await service.GetByIdAsync(4));
    }

    [Fact]
    public async Task UpdateAsync_WhenQuoteDoesNotExist_ReturnsNull()
    {
        // Arrange
        var service = new InMemoryQuoteService();
        var request = new UpdateQuoteRequest("Updated text", "Author", "General");

        // Act
        var result = await service.UpdateAsync(999, request);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_HidesQuoteFromSubsequentReads()
    {
        // Arrange
        var service = new InMemoryQuoteService();

        // Act
        var deleted = await service.DeleteAsync(1);
        var fetched = await service.GetByIdAsync(1);
        var remaining = await service.GetAllAsync();

        // Assert
        Assert.True(deleted);
        Assert.Null(fetched);
        Assert.Equal(2, remaining.Count);
        Assert.False(await service.DeleteAsync(1)); // Deleting twice returns false
    }

    [Fact]
    public async Task SearchAsync_MatchesTextOrAuthor_CaseInsensitively()
    {
        // Arrange
        var service = new InMemoryQuoteService();

        // Act
        var results = await service.SearchAsync("hopper");

        // Assert
        var match = Assert.Single(results);
        Assert.Equal("Grace Hopper", match.Author);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchAsync_WhenQueryIsBlank_ReturnsEmpty(string blankQuery)
    {
        // Arrange
        var service = new InMemoryQuoteService();

        // Act
        var results = await service.SearchAsync(blankQuery);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_ExcludesDeletedQuotes()
    {
        // Arrange
        var service = new InMemoryQuoteService();
        await service.DeleteAsync(2); // Deletes Grace Hopper's quote

        // Act
        var results = await service.SearchAsync("hopper");

        // Assert
        Assert.Empty(results);
    }
}
