using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using QuoteHub.Api.Data;
using QuoteHub.Api.Middleware;
using QuoteHub.Api.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Bind to Railway / Azure Container Apps injected PORT when running in containers
if (Environment.GetEnvironmentVariable("PORT") is { Length: > 0 } port)
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// Core API, OpenAPI & Health Checks
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<QuoteDbContext>(name: "database");

// Global RFC 7807 ProblemDetails exception handling
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// JWT Authentication & Role-Based Authorization
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? "QuoteHubDotNet-SuperSecret-DevKey-ChangeInProduction-2026!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "QuoteHub.Api";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "QuoteHub.Clients";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

// Built-in Rate Limiting (fixed window per client IP -> 429 Too Many Requests)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var key = httpContext.Connection.RemoteIpAddress?.ToString() ?? "local";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: key,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });
});

// Built-in Output Caching (30s TTL on GET list, evicted on writes)
builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("QuotesList", policy => policy
        .Expire(TimeSpan.FromSeconds(30))
        .SetVaryByQuery("category", "tag", "page", "pageSize")
        .Tag("quotes"));
});

// OpenTelemetry Distributed Tracing (exports via OTLP when OTEL_EXPORTER_OTLP_ENDPOINT is set)
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("QuoteHub.Api"))
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation();
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")))
        {
            tracing.AddOtlpExporter();
        }
    });

// Data Access Layer: PostgreSQL when DATABASE_URL is set (Railway/cloud), SQLite locally
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
builder.Services.AddDbContext<QuoteDbContext>(options =>
{
    if (!string.IsNullOrWhiteSpace(databaseUrl))
    {
        options.UseNpgsql(BuildNpgsqlConnectionString(databaseUrl), npgsqlOptions =>
        {
            // Enterprise Resiliency: automatically retries transient database connection failures
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorCodesToAdd: null);
        });
    }
    else
    {
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
    }
});

// Business Logic & Background Services
builder.Services.AddScoped<IQuoteService, EfQuoteService>();
builder.Services.AddSingleton<ITokenService, JwtTokenService>();
builder.Services.AddHostedService<SoftDeleteCleanupService>();

var app = builder.Build();

// Ensure database schema + seed data exist on startup (skipped in Testing where tests manage their own DB)
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<QuoteDbContext>();
    db.Database.EnsureCreated();

    // Idempotent schema upgrade for existing PostgreSQL instances (e.g., Railway upgrades)
    if (!string.IsNullOrWhiteSpace(databaseUrl))
    {
        db.Database.ExecuteSqlRaw("""
            ALTER TABLE "Quotes" ADD COLUMN IF NOT EXISTS "DeletedAtUtc" timestamp with time zone NULL;
            CREATE TABLE IF NOT EXISTS "Tags" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "Name" character varying(40) NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Tags_Name" ON "Tags" ("Name");
            CREATE TABLE IF NOT EXISTS "QuoteTags" (
                "QuotesId" integer NOT NULL REFERENCES "Quotes" ("Id") ON DELETE CASCADE,
                "TagsId" integer NOT NULL REFERENCES "Tags" ("Id") ON DELETE CASCADE,
                PRIMARY KEY ("QuotesId", "TagsId")
            );
            CREATE INDEX IF NOT EXISTS "IX_QuoteTags_TagsId" ON "QuoteTags" ("TagsId");
            """);
    }
}

// HTTP request pipeline (order matters!)
app.UseExceptionHandler();
app.UseRateLimiter();
app.UseOutputCache();

app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.Title = "QuoteHub API v1";
    options.DefaultHttpClient = new(ScalarTarget.CSharp, ScalarClient.HttpClient);
});

app.MapGet("/", () => Results.Redirect("/scalar/v1")).ExcludeFromDescription();

app.UseAuthentication();
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
