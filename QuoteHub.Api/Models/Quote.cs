namespace QuoteHub.Api.Models;

public class Quote
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Internal-only field (for soft deletes) — never exposed in API responses
    public bool IsDeleted { get; set; }
}
