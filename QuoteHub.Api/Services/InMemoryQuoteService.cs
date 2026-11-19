using QuoteHub.Api.Dtos;
using QuoteHub.Api.Models;

namespace QuoteHub.Api.Services;

public class InMemoryQuoteService : IQuoteService
{
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

    public IReadOnlyList<QuoteResponse> GetAll(string? category = null)
    {
        lock (_lock)
        {
            var query = _quotes.Where(q => !q.IsDeleted);

            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(q =>
                    q.Category.Equals(category.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            return query.Select(ToResponse).ToList();
        }
    }

    public QuoteResponse? GetById(int id)
    {
        lock (_lock)
        {
            var quote = _quotes.FirstOrDefault(q => q.Id == id && !q.IsDeleted);
            return quote is null ? null : ToResponse(quote);
        }
    }

    public QuoteResponse? GetRandom()
    {
        lock (_lock)
        {
            var active = _quotes.Where(q => !q.IsDeleted).ToList();
            if (active.Count == 0)
            {
                return null;
            }

            var pick = active[Random.Shared.Next(active.Count)];
            return ToResponse(pick);
        }
    }

    public IReadOnlyList<QuoteResponse> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var term = query.Trim();

        lock (_lock)
        {
            return _quotes
                .Where(q => !q.IsDeleted &&
                    (q.Text.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                     q.Author.Contains(term, StringComparison.OrdinalIgnoreCase)))
                .Select(ToResponse)
                .ToList();
        }
    }

    public QuoteResponse Create(CreateQuoteRequest request)
    {
        lock (_lock)
        {
            var quote = new Quote
            {
                Id = _nextId++,
                Text = request.Text.Trim(),
                Author = request.Author.Trim(),
                Category = string.IsNullOrWhiteSpace(request.Category)
                    ? "General"
                    : request.Category.Trim(),
                CreatedAtUtc = DateTime.UtcNow
            };

            _quotes.Add(quote);
            return ToResponse(quote);
        }
    }

    public QuoteResponse? Update(int id, UpdateQuoteRequest request)
    {
        lock (_lock)
        {
            var quote = _quotes.FirstOrDefault(q => q.Id == id && !q.IsDeleted);
            if (quote is null)
            {
                return null;
            }

            quote.Text = request.Text.Trim();
            quote.Author = request.Author.Trim();
            quote.Category = request.Category.Trim();

            return ToResponse(quote);
        }
    }

    public bool Delete(int id)
    {
        lock (_lock)
        {
            var quote = _quotes.FirstOrDefault(q => q.Id == id && !q.IsDeleted);
            if (quote is null)
            {
                return false;
            }

            // Soft delete — keeps audit history while hiding it from API consumers
            quote.IsDeleted = true;
            return true;
        }
    }

    private static QuoteResponse ToResponse(Quote quote) =>
        new(quote.Id, quote.Text, quote.Author, quote.Category, quote.CreatedAtUtc);
}
