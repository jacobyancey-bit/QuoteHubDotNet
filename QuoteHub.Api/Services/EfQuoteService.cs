using Microsoft.EntityFrameworkCore;
using QuoteHub.Api.Data;
using QuoteHub.Api.Dtos;
using QuoteHub.Api.Models;

namespace QuoteHub.Api.Services;

public class EfQuoteService(QuoteDbContext db) : IQuoteService
{
    public async Task<IReadOnlyList<QuoteResponse>> GetAllAsync(string? category = null, CancellationToken ct = default)
    {
        var query = db.Quotes.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(category))
        {
            var normalized = category.Trim().ToLower();
            query = query.Where(q => q.Category.ToLower() == normalized);
        }

        return await query
            .OrderBy(q => q.Id)
            .Select(q => new QuoteResponse(q.Id, q.Text, q.Author, q.Category, q.CreatedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<QuoteResponse?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await db.Quotes
            .AsNoTracking()
            .Where(q => q.Id == id)
            .Select(q => new QuoteResponse(q.Id, q.Text, q.Author, q.Category, q.CreatedAtUtc))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<QuoteResponse?> GetRandomAsync(CancellationToken ct = default)
    {
        var count = await db.Quotes.CountAsync(ct);
        if (count == 0)
        {
            return null;
        }

        var skip = Random.Shared.Next(count);

        return await db.Quotes
            .AsNoTracking()
            .OrderBy(q => q.Id)
            .Skip(skip)
            .Select(q => new QuoteResponse(q.Id, q.Text, q.Author, q.Category, q.CreatedAtUtc))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<QuoteResponse>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var pattern = $"%{query.Trim()}%";

        return await db.Quotes
            .AsNoTracking()
            .Where(q => EF.Functions.Like(q.Text, pattern) || EF.Functions.Like(q.Author, pattern))
            .OrderBy(q => q.Id)
            .Select(q => new QuoteResponse(q.Id, q.Text, q.Author, q.Category, q.CreatedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<QuoteResponse> CreateAsync(CreateQuoteRequest request, CancellationToken ct = default)
    {
        var quote = new Quote
        {
            Text = request.Text.Trim(),
            Author = request.Author.Trim(),
            Category = string.IsNullOrWhiteSpace(request.Category)
                ? "General"
                : request.Category.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Quotes.Add(quote);
        await db.SaveChangesAsync(ct);

        return ToResponse(quote);
    }

    public async Task<QuoteResponse?> UpdateAsync(int id, UpdateQuoteRequest request, CancellationToken ct = default)
    {
        // Tracked query (no AsNoTracking) so EF Core detects changes on SaveChangesAsync
        var quote = await db.Quotes.FirstOrDefaultAsync(q => q.Id == id, ct);
        if (quote is null)
        {
            return null;
        }

        quote.Text = request.Text.Trim();
        quote.Author = request.Author.Trim();
        quote.Category = request.Category.Trim();

        await db.SaveChangesAsync(ct);
        return ToResponse(quote);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var quote = await db.Quotes.FirstOrDefaultAsync(q => q.Id == id, ct);
        if (quote is null)
        {
            return false;
        }

        quote.IsDeleted = true;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static QuoteResponse ToResponse(Quote quote) =>
        new(quote.Id, quote.Text, quote.Author, quote.Category, quote.CreatedAtUtc);
}
