using Microsoft.EntityFrameworkCore;
using QuoteHub.Api.Data;
using QuoteHub.Api.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Register EF Core DbContext (automatically registered as Scoped!)
builder.Services.AddDbContext<QuoteDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register our EF Core-backed service as Scoped (matches QuoteDbContext's Scoped lifetime).
builder.Services.AddScoped<IQuoteService, EfQuoteService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "QuoteHub API";
        options.DefaultHttpClient = new(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
