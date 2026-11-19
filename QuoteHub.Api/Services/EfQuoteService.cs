using Microsoft.EntityFrameworkCore;
using QuoteHub.Api.Data;
using QuoteHub.Api.Dtos;
using QuoteHub.Api.Models;

namespace QuoteHub.Api.Services;

public class EfQuoteService(QuoteDbContext db, ILogger<EfQuoteService> logger) : IQuoteService
{
    private const int MaxPageSize = 100;

    public async Task<PagedResult<QuoteResponse>> GetAllAsync(
        string? category = null,
        string? tag = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = db.Quotes.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(category))
        {
            var normalizedCategory = category.Trim().ToLower();
            query = query.Where(q => q.Category.ToLower() == normalizedCategory);
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            var normalizedTag = tag.Trim().ToLower();
            query = query.Where(q => q.Tags.Any(t => t.Name == normalizedTag));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(q => q.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(q => q.Tags)
            .Select(q => ToResponse(q))
            .ToListAsync(ct);

        return new PagedResult<QuoteResponse>(items, page, pageSize, totalCount);
    }

    public async Task<QuoteResponse?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var quote = await db.Quotes
            .AsNoTracking()
            .Include(q => q.Tags)
            .FirstOrDefaultAsync(q => q.Id == id, ct);

        return quote is null ? null : ToResponse(quote);
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
            .Select(q => ToResponse(q))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<QuoteResponse?> GetDailyQuoteAsync(CancellationToken ct = default)
    {
        var count = await db.Quotes.CountAsync(ct);
        if (count == 0)
        {
            return null;
        }

        var today = DateTime.UtcNow.Date;
        var seed = today.Year * 10000 + today.Month * 100 + today.Day;
        var dayIndex = Math.Abs(seed) % count;

        return await db.Quotes
            .AsNoTracking()
            .OrderBy(q => q.Id)
            .Skip(dayIndex)
            .Select(q => ToResponse(q))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<QuoteResponse>> GetTopQuotesAsync(int limit = 10, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 50);

        return await db.Quotes
            .AsNoTracking()
            .OrderByDescending(q => q.LikesCount)
            .ThenBy(q => q.Id)
            .Take(limit)
            .Select(q => ToResponse(q))
            .ToListAsync(ct);
    }

    public async Task<int?> LikeQuoteAsync(int id, CancellationToken ct = default)
    {
        var quote = await db.Quotes.FirstOrDefaultAsync(q => q.Id == id, ct);
        if (quote is null)
        {
            return null;
        }

        quote.LikesCount++;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Liked quote {QuoteId}. New LikesCount: {Likes}", quote.Id, quote.LikesCount);
        return quote.LikesCount;
    }

    public async Task<IReadOnlyList<QuoteResponse>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var pattern = $"%{query.Trim().ToLower()}%";

        return await db.Quotes
            .AsNoTracking()
            .Where(q => EF.Functions.Like(q.Text.ToLower(), pattern) || EF.Functions.Like(q.Author.ToLower(), pattern))
            .OrderBy(q => q.Id)
            .Select(q => ToResponse(q))
            .ToListAsync(ct);
    }

    public async Task<QuoteResponse> CreateAsync(CreateQuoteRequest request, CancellationToken ct = default)
    {
        var tags = await ResolveTagsAsync(request.Tags, ct);

        var quote = new Quote
        {
            Text = request.Text.Trim(),
            Author = request.Author.Trim(),
            Category = string.IsNullOrWhiteSpace(request.Category)
                ? "General"
                : request.Category.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            Tags = tags
        };

        db.Quotes.Add(quote);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Created quote {QuoteId} by {Author} in category {Category}",
            quote.Id, quote.Author, quote.Category);

        return ToResponse(quote);
    }

    public async Task<QuoteResponse?> UpdateAsync(int id, UpdateQuoteRequest request, CancellationToken ct = default)
    {
        var quote = await db.Quotes
            .Include(q => q.Tags)
            .FirstOrDefaultAsync(q => q.Id == id, ct);

        if (quote is null)
        {
            return null;
        }

        quote.Text = request.Text.Trim();
        quote.Author = request.Author.Trim();
        quote.Category = request.Category.Trim();

        if (request.Tags is not null)
        {
            quote.Tags.Clear();
            foreach (var tag in await ResolveTagsAsync(request.Tags, ct))
            {
                quote.Tags.Add(tag);
            }
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation("Updated quote {QuoteId}", quote.Id);
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
        quote.DeletedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Soft-deleted quote {QuoteId}", quote.Id);
        return true;
    }

    private async Task<List<Tag>> ResolveTagsAsync(IReadOnlyList<string>? rawTags, CancellationToken ct)
    {
        if (rawTags is null || rawTags.Count == 0)
        {
            return [];
        }

        var names = rawTags
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim().ToLower())
            .Distinct()
            .ToList();

        if (names.Count == 0)
        {
            return [];
        }

        var existing = await db.Tags
            .Where(t => names.Contains(t.Name))
            .ToListAsync(ct);

        var existingNames = existing.Select(t => t.Name).ToHashSet();

        foreach (var name in names.Where(n => !existingNames.Contains(n)))
        {
            var created = new Tag { Name = name };
            db.Tags.Add(created);
            existing.Add(created);
        }

        return existing;
    }

    private static QuoteResponse ToResponse(Quote quote) =>
        new(
            quote.Id,
            quote.Text,
            quote.Author,
            quote.Category,
            quote.CreatedAtUtc,
            quote.Tags.OrderBy(t => t.Name).Select(t => t.Name).ToList(),
            quote.LikesCount);
}
