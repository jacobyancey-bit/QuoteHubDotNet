using QuoteHub.Api.Dtos;
using QuoteHub.Api.Services;

namespace QuoteHub.Tests;

public class InMemoryQuoteServiceTests
{
    [Fact]
    public void GetAll_ReturnsSeededQuotes()
    {
        // Arrange
        var service = new InMemoryQuoteService();

        // Act
        var quotes = service.GetAll();

        // Assert
        Assert.Equal(3, quotes.Count);
    }

    [Fact]
    public void GetAll_WithCategoryFilter_IsCaseInsensitive()
    {
        // Arrange
        var service = new InMemoryQuoteService();

        // Act
        var spaceQuotes = service.GetAll("space");

        // Assert
        var single = Assert.Single(spaceQuotes);
        Assert.Equal("Katherine Johnson", single.Author);
    }

    [Fact]
    public void Create_AssignsNextIdAndTrimsInput()
    {
        // Arrange
        var service = new InMemoryQuoteService();
        var request = new CreateQuoteRequest("  Failure is not an option.  ", "  Gene Kranz  ", "  Space  ");

        // Act
        var created = service.Create(request);

        // Assert
        Assert.Equal(4, created.Id);
        Assert.Equal("Failure is not an option.", created.Text);
        Assert.Equal("Gene Kranz", created.Author);
        Assert.Equal("Space", created.Category);
        Assert.NotNull(service.GetById(4));
    }

    [Fact]
    public void Update_WhenQuoteDoesNotExist_ReturnsNull()
    {
        // Arrange
        var service = new InMemoryQuoteService();
        var request = new UpdateQuoteRequest("Updated text", "Author", "General");

        // Act
        var result = service.Update(999, request);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Delete_HidesQuoteFromSubsequentReads()
    {
        // Arrange
        var service = new InMemoryQuoteService();

        // Act
        var deleted = service.Delete(1);
        var fetched = service.GetById(1);
        var remaining = service.GetAll();

        // Assert
        Assert.True(deleted);
        Assert.Null(fetched);
        Assert.Equal(2, remaining.Count);
        Assert.False(service.Delete(1)); // Deleting twice returns false
    }

    [Fact]
    public void Search_MatchesTextOrAuthor_CaseInsensitively()
    {
        // Arrange
        var service = new InMemoryQuoteService();

        // Act
        var results = service.Search("hopper");

        // Assert
        var match = Assert.Single(results);
        Assert.Equal("Grace Hopper", match.Author);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Search_WhenQueryIsBlank_ReturnsEmpty(string blankQuery)
    {
        // Arrange
        var service = new InMemoryQuoteService();

        // Act
        var results = service.Search(blankQuery);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void Search_ExcludesDeletedQuotes()
    {
        // Arrange
        var service = new InMemoryQuoteService();
        service.Delete(2); // Deletes Grace Hopper's quote

        // Act
        var results = service.Search("hopper");

        // Assert
        Assert.Empty(results);
    }
}
