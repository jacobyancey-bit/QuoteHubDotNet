using QuoteHub.Api.Dtos;
using QuoteHub.Api.Models;

namespace QuoteHub.Api.Services;

public class InMemoryQuoteService : IQuoteService
{
    private const int MaxPageSize = 100;
    private readonly Lock _lock = new();
    private int _nextId = 4;

    private readonly List<Quote> _quotes =
    [
        new Quote
        {
            Id = 1,
            Text = "Like what you do, and then you will do your best.",
            Author = "Katherine Johnson",
            Category = "Space",
            CreatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        },
        new Quote
        {
            Id = 2,
            Text = "The most dangerous phrase in the language is, 'We've always done it this way.'",
            Author = "Grace Hopper",
            Category = "Engineering",
            CreatedAtUtc = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)
        },
        new Quote
        {
            Id = 3,
            Text = "Simplicity is prerequisite for reliability.",
            Author = "Edsger W. Dijkstra",
            Category = "Engineering",
            CreatedAtUtc = new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc)
        }
    ];

    public Task<PagedResult<QuoteResponse>> GetAllAsync(
        string? category = null,
        string? tag = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        lock (_lock)
        {
            var query = _quotes.Where(q => !q.IsDeleted);

            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(q =>
                    q.Category.Equals(category.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(tag))
            {
                var normalizedTag = tag.Trim().ToLower();
                query = query.Where(q => q.Tags.Any(t => t.Name == normalizedTag));
            }

            var matched = query.OrderBy(q => q.Id).ToList();
            var items = matched
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(ToResponse)
                .ToList();

            return Task.FromResult(new PagedResult<QuoteResponse>(items, page, pageSize, matched.Count));
        }
    }

    public Task<QuoteResponse?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var quote = _quotes.FirstOrDefault(q => q.Id == id && !q.IsDeleted);
            return Task.FromResult(quote is null ? null : ToResponse(quote));
        }
    }

    public Task<QuoteResponse?> GetRandomAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            var active = _quotes.Where(q => !q.IsDeleted).ToList();
            if (active.Count == 0)
            {
                return Task.FromResult<QuoteResponse?>(null);
            }

            var pick = active[Random.Shared.Next(active.Count)];
            return Task.FromResult<QuoteResponse?>(ToResponse(pick));
        }
    }

    public Task<IReadOnlyList<QuoteResponse>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult<IReadOnlyList<QuoteResponse>>([]);
        }

        var term = query.Trim();

        lock (_lock)
        {
            IReadOnlyList<QuoteResponse> results = _quotes
                .Where(q => !q.IsDeleted &&
                    (q.Text.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                     q.Author.Contains(term, StringComparison.OrdinalIgnoreCase)))
                .Select(ToResponse)
                .ToList();

            return Task.FromResult(results);
        }
    }

    public Task<QuoteResponse> CreateAsync(CreateQuoteRequest request, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var tags = (request.Tags ?? [])
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim().ToLower())
                .Distinct()
                .Select(name => new Tag { Name = name })
                .ToList();

            var quote = new Quote
            {
                Id = _nextId++,
                Text = request.Text.Trim(),
                Author = request.Author.Trim(),
                Category = string.IsNullOrWhiteSpace(request.Category)
                    ? "General"
                    : request.Category.Trim(),
                CreatedAtUtc = DateTime.UtcNow,
                Tags = tags
            };

            _quotes.Add(quote);
            return Task.FromResult(ToResponse(quote));
        }
    }

    public Task<QuoteResponse?> UpdateAsync(int id, UpdateQuoteRequest request, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var quote = _quotes.FirstOrDefault(q => q.Id == id && !q.IsDeleted);
            if (quote is null)
            {
                return Task.FromResult<QuoteResponse?>(null);
            }

            quote.Text = request.Text.Trim();
            quote.Author = request.Author.Trim();
            quote.Category = request.Category.Trim();

            if (request.Tags is not null)
            {
                quote.Tags = request.Tags
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Select(t => t.Trim().ToLower())
                    .Distinct()
                    .Select(name => new Tag { Name = name })
                    .ToList();
            }

            return Task.FromResult<QuoteResponse?>(ToResponse(quote));
        }
    }

    public Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var quote = _quotes.FirstOrDefault(q => q.Id == id && !q.IsDeleted);
            if (quote is null)
            {
                return Task.FromResult(false);
            }

            quote.IsDeleted = true;
            quote.DeletedAtUtc = DateTime.UtcNow;
            return Task.FromResult(true);
        }
    }

    private static QuoteResponse ToResponse(Quote quote) =>
        new(
            quote.Id,
            quote.Text,
            quote.Author,
            quote.Category,
            quote.CreatedAtUtc,
            quote.Tags.OrderBy(t => t.Name).Select(t => t.Name).ToList());
}
