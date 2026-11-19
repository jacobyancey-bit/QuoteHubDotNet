using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
        var page = await _service.GetAllAsync(category: "SPACE");

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
    public async Task CreateAsync_WithTags_PersistsManyToManyAndFiltersByTag()
    {
        var request = new CreateQuoteRequest(
            "  Failure is not an option.  ",
            "  Gene Kranz  ",
            "Space",
            ["NASA", "Leadership", "nasa"]); // duplicate + mixed case should normalize

        var created = await _service.CreateAsync(request);

        Assert.Equal(["leadership", "nasa"], created.Tags);

        var filtered = await _service.GetAllAsync(tag: "NASA");
        var match = Assert.Single(filtered.Items);
        Assert.Equal(created.Id, match.Id);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesExistingRowAndTagsInDatabase()
    {
        var updated = await _service.UpdateAsync(1, new UpdateQuoteRequest(
            "Updated quote text",
            "Katherine Johnson",
            "NASA",
            ["apollo", "math"]));

        Assert.NotNull(updated);
        Assert.Equal("NASA", updated.Category);
        Assert.Equal(["apollo", "math"], updated.Tags);

        var reloaded = await _service.GetByIdAsync(1);
        Assert.Equal("Updated quote text", reloaded?.Text);
        Assert.Equal(["apollo", "math"], reloaded?.Tags);
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
        Assert.NotNull(rawRow.DeletedAtUtc);
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

    [Fact]
    public async Task SoftDeleteCleanupService_PurgesOnlyExpiredDeletedQuotes()
    {
        // Soft-delete #1 (recent) and #2 (expired 45 days ago)
        await _service.DeleteAsync(1);
        await _service.DeleteAsync(2);

        var expiredRow = await _db.Quotes.IgnoreQueryFilters().FirstAsync(q => q.Id == 2);
        expiredRow.DeletedAtUtc = DateTime.UtcNow.AddDays(-45);
        await _db.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddDbContext<QuoteDbContext>(o => o.UseSqlite(_connection));
        await using var sp = services.BuildServiceProvider();

        var worker = new SoftDeleteCleanupService(
            sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<SoftDeleteCleanupService>.Instance);

        var purged = await worker.PurgeExpiredQuotesAsync();

        Assert.Equal(1, purged);
        Assert.Null(await _db.Quotes.IgnoreQueryFilters().FirstOrDefaultAsync(q => q.Id == 2));
        Assert.NotNull(await _db.Quotes.IgnoreQueryFilters().FirstOrDefaultAsync(q => q.Id == 1));
    }
}
