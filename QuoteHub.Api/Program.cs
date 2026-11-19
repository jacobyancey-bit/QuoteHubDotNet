using Microsoft.EntityFrameworkCore;
using QuoteHub.Api.Data;
using QuoteHub.Api.Middleware;
using QuoteHub.Api.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Core API & OpenAPI services
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

// Global RFC 7807 ProblemDetails exception handling
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Data Access Layer (EF Core + SQLite, registered as Scoped)
builder.Services.AddDbContext<QuoteDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Business Logic Layer (Scoped to match QuoteDbContext's lifetime)
builder.Services.AddScoped<IQuoteService, EfQuoteService>();

var app = builder.Build();

// HTTP request pipeline (order matters!)
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "QuoteHub API";
        options.DefaultHttpClient = new(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}
else
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();

// Expose the implicit Program class so WebApplicationFactory<Program> can boot the API in integration tests.
public partial class Program;
