using System.Net;
using System.Net.Http.Headers;
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

    private async Task<string> GetTokenAsync(string username, string password)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/token",
            new LoginRequest(username, password));

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(payload);
        return payload.AccessToken;
    }

    [Fact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetDaily_ReturnsConsistentQuoteForToday()
    {
        var response1 = await _client.GetAsync("/api/v1/quotes/daily");
        var response2 = await _client.GetAsync("/api/v1/quotes/daily");

        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);

        var quote1 = await response1.Content.ReadFromJsonAsync<QuoteResponse>();
        var quote2 = await response2.Content.ReadFromJsonAsync<QuoteResponse>();

        Assert.NotNull(quote1);
        Assert.NotNull(quote2);
        Assert.Equal(quote1.Id, quote2.Id);
    }

    [Fact]
    public async Task GetAll_SupportsV1RouteAndPagination()
    {
        var response = await _client.GetAsync("/api/v1/quotes?page=1&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedResult<QuoteResponse>>();
        Assert.NotNull(page);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.Equal(2, page.Items.Count);
    }

    [Fact]
    public async Task WriteEndpoints_EnforceJwtAuthenticationAndAdminRole()
    {
        var payload = new CreateQuoteRequest("Unauthorized attempt", "Tester", "Test");

        // 1. No token -> 401 Unauthorized
        var unauthResponse = await _client.PostAsJsonAsync("/api/v1/quotes", payload);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthResponse.StatusCode);

        // 2. Reader role token -> 403 Forbidden
        var readerToken = await GetTokenAsync("reader", "QuoteHubReader!2026");
        using var readerReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/quotes")
        {
            Content = JsonContent.Create(payload)
        };
        readerReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", readerToken);
        var forbiddenResponse = await _client.SendAsync(readerReq);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
    }

    [Fact]
    public async Task Create_WithAdminTokenAndInvalidPayload_Returns400ProblemDetails()
    {
        var adminToken = await GetTokenAsync("admin", "QuoteHubAdmin!2026");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var invalid = new CreateQuoteRequest("Hi", "");
        var response = await _client.PostAsJsonAsync("/api/v1/quotes", invalid);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task FullCrudLifecycle_WithTagsAndAdminJwt_WorksEndToEnd()
    {
        var adminToken = await GetTokenAsync("admin", "QuoteHubAdmin!2026");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // 1. POST -> 201 Created + Location header + Tags
        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/quotes",
            new CreateQuoteRequest("We choose to go to the Moon.", "John F. Kennedy", "Space", ["apollo", "nasa"]));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(createResponse.Headers.Location);

        var created = await createResponse.Content.ReadFromJsonAsync<QuoteResponse>();
        Assert.NotNull(created);
        Assert.Equal(["apollo", "nasa"], created.Tags);

        // 2. Filter by tag (verifies OutputCache eviction + tag query)
        var taggedPage = await _client.GetFromJsonAsync<PagedResult<QuoteResponse>>("/api/v1/quotes?tag=apollo");
        Assert.NotNull(taggedPage);
        var taggedMatch = Assert.Single(taggedPage.Items);
        Assert.Equal(created.Id, taggedMatch.Id);

        // 3. PUT -> 200 OK
        var updateResponse = await _client.PutAsJsonAsync(
            $"/api/v1/quotes/{created.Id}",
            new UpdateQuoteRequest("We choose to go to the Moon in this decade.", "JFK", "History", ["speech"]));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        // 4. DELETE -> 204 No Content
        var deleteResponse = await _client.DeleteAsync($"/api/v1/quotes/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // 5. GET after DELETE -> 404 Not Found
        var afterDelete = await _client.GetAsync($"/api/v1/quotes/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    [Fact]
    public async Task Search_ReturnsMatches_And400WhenQueryMissing()
    {
        var badResponse = await _client.GetAsync("/api/v1/quotes/search");
        Assert.Equal(HttpStatusCode.BadRequest, badResponse.StatusCode);

        var results = await _client.GetFromJsonAsync<List<QuoteResponse>>("/api/v1/quotes/search?q=hopper");
        Assert.NotNull(results);
        var match = Assert.Single(results);
        Assert.Equal("Grace Hopper", match.Author);
    }
}
