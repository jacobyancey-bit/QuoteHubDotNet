using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuoteHub.Api.Data;
using QuoteHub.Api.Dtos;

namespace QuoteHub.Tests;

/// <summary>
/// Full HTTP end-to-end integration tests booting the real ASP.NET Core pipeline
/// against an isolated in-memory SQLite database.
/// </summary>
public class QuotesApiIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public QuotesApiIntegrationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                // Replace the file-based SQLite DbContext with our ephemeral in-memory SQLite connection
                services.RemoveAll<DbContextOptions<QuoteDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<QuoteDbContext>>();
                services.AddDbContext<QuoteDbContext>(options => options.UseSqlite(_connection));

                using var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<QuoteDbContext>();
                db.Database.EnsureCreated();
            });
        });

        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetAll_ReturnsPagedSeededQuotes()
    {
        var response = await _client.GetAsync("/api/quotes?page=1&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedResult<QuoteResponse>>();
        Assert.NotNull(page);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.Equal(2, page.Items.Count);
    }

    [Fact]
    public async Task Create_WithInvalidPayload_Returns400ProblemDetails()
    {
        var invalid = new CreateQuoteRequest("Hi", "");

        var response = await _client.PostAsJsonAsync("/api/quotes", invalid);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task FullCrudLifecycle_WorksEndToEndOverHttp()
    {
        // 1. POST -> 201 Created + Location header
        var createResponse = await _client.PostAsJsonAsync(
            "/api/quotes",
            new CreateQuoteRequest("We choose to go to the Moon.", "John F. Kennedy", "Space"));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(createResponse.Headers.Location);

        var created = await createResponse.Content.ReadFromJsonAsync<QuoteResponse>();
        Assert.NotNull(created);

        // 2. GET -> 200 OK
        var fetched = await _client.GetFromJsonAsync<QuoteResponse>($"/api/quotes/{created.Id}");
        Assert.Equal("John F. Kennedy", fetched?.Author);

        // 3. PUT -> 200 OK
        var updateResponse = await _client.PutAsJsonAsync(
            $"/api/quotes/{created.Id}",
            new UpdateQuoteRequest("We choose to go to the Moon in this decade.", "JFK", "History"));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        // 4. DELETE -> 204 No Content
        var deleteResponse = await _client.DeleteAsync($"/api/quotes/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // 5. GET after DELETE -> 404 Not Found
        var afterDelete = await _client.GetAsync($"/api/quotes/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    [Fact]
    public async Task Search_ReturnsMatches_And400WhenQueryMissing()
    {
        var badResponse = await _client.GetAsync("/api/quotes/search");
        Assert.Equal(HttpStatusCode.BadRequest, badResponse.StatusCode);

        var results = await _client.GetFromJsonAsync<List<QuoteResponse>>("/api/quotes/search?q=hopper");
        Assert.NotNull(results);
        var match = Assert.Single(results);
        Assert.Equal("Grace Hopper", match.Author);
    }
}
