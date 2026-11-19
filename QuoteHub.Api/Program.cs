using Microsoft.EntityFrameworkCore;
using Npgsql;
using QuoteHub.Api.Data;
using QuoteHub.Api.Middleware;
using QuoteHub.Api.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Bind to Railway's injected PORT when running in containers
if (Environment.GetEnvironmentVariable("PORT") is { Length: > 0 } port)
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// Core API & OpenAPI services
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

// Global RFC 7807 ProblemDetails exception handling
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Data Access Layer: PostgreSQL when DATABASE_URL is set (Railway/cloud), SQLite locally
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
builder.Services.AddDbContext<QuoteDbContext>(options =>
{
    if (!string.IsNullOrWhiteSpace(databaseUrl))
    {
        options.UseNpgsql(BuildNpgsqlConnectionString(databaseUrl));
    }
    else
    {
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
    }
});

// Business Logic Layer (Scoped to match QuoteDbContext's lifetime)
builder.Services.AddScoped<IQuoteService, EfQuoteService>();

var app = builder.Build();

// Ensure database schema + seed data exist on startup (skipped in Testing where tests manage their own DB)
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<QuoteDbContext>();
    db.Database.EnsureCreated();
}

// HTTP request pipeline
app.UseExceptionHandler();

app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.Title = "QuoteHub API";
    options.DefaultHttpClient = new(ScalarTarget.CSharp, ScalarClient.HttpClient);
});

app.MapGet("/", () => Results.Redirect("/scalar/v1")).ExcludeFromDescription();

app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();

static string BuildNpgsqlConnectionString(string databaseUrl)
{
    var uri = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':', 2);
    var csb = new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.Port > 0 ? uri.Port : 5432,
        Username = Uri.UnescapeDataString(userInfo[0]),
        Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty,
        Database = uri.AbsolutePath.TrimStart('/'),
        SslMode = SslMode.Prefer
    };
    return csb.ConnectionString;
}

// Expose the implicit Program class so WebApplicationFactory<Program> can boot the API in integration tests.
public partial class Program;
