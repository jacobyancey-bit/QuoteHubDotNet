using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuoteHub.Api.Data;
using QuoteHub.Api.Dtos;
using QuoteHub.Api.Services;

namespace QuoteHub.Tests;

public class EfQuoteServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly QuoteDbContext _db;
    private readonly EfQuoteService _service;

    public EfQuoteServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<QuoteDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new QuoteDbContext(options);
        _db.Database.EnsureCreated();

        _service = new EfQuoteService(_db, NullLogger<EfQuoteService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsSeededQuotesFromDatabase()
    {
        var page = await _service.GetAllAsync();

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(3, page.Items.Count);
    }

    [Fact]
    public async Task GetAllAsync_FiltersByCategoryCaseInsensitively()
    {
        var page = await _service.GetAllAsync("SPACE");

        var single = Assert.Single(page.Items);
        Assert.Equal("Katherine Johnson", single.Author);
    }

    [Fact]
    public async Task GetAllAsync_PaginatesInSql()
    {
        var page1 = await _service.GetAllAsync(page: 1, pageSize: 2);
        var page2 = await _service.GetAllAsync(page: 2, pageSize: 2);

        Assert.Equal(3, page1.TotalCount);
        Assert.Equal(2, page1.TotalPages);
        Assert.Equal(2, page1.Items.Count);
        Assert.Single(page2.Items);
    }

    [Fact]
    public async Task CreateAsync_PersistsQuoteToDatabase()
    {
        var request = new CreateQuoteRequest("  Failure is not an option.  ", "  Gene Kranz  ", "Space");

        var created = await _service.CreateAsync(request);
        var fetched = await _service.GetByIdAsync(created.Id);

        Assert.NotNull(fetched);
        Assert.Equal("Failure is not an option.", fetched.Text);
        Assert.Equal("Gene Kranz", fetched.Author);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesExistingRowInDatabase()
    {
        var updated = await _service.UpdateAsync(1, new UpdateQuoteRequest(
            "Updated quote text",
            "Katherine Johnson",
            "NASA"));

        Assert.NotNull(updated);
        Assert.Equal("NASA", updated.Category);

        var reloaded = await _service.GetByIdAsync(1);
        Assert.Equal("Updated quote text", reloaded?.Text);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesRow_HiddenByGlobalQueryFilter()
    {
        var deleted = await _service.DeleteAsync(2);

        Assert.True(deleted);
        Assert.Null(await _service.GetByIdAsync(2));

        var rawRow = await _db.Quotes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(q => q.Id == 2);

        Assert.NotNull(rawRow);
        Assert.True(rawRow.IsDeleted);
    }

    [Fact]
    public async Task SearchAsync_MatchesViaSqlLikeAndExcludesDeleted()
    {
        await _service.DeleteAsync(2);

        var hopperResults = await _service.SearchAsync("hopper");
        var simplicityResults = await _service.SearchAsync("simplicity");

        Assert.Empty(hopperResults);
        var match = Assert.Single(simplicityResults);
        Assert.Equal("Edsger W. Dijkstra", match.Author);
    }
}
