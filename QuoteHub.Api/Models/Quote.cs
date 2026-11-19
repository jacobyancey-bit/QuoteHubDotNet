namespace QuoteHub.Api.Models;

public class Quote
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Upvote / Like count for community rankings
    public int LikesCount { get; set; }

    // Internal-only soft-delete fields
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }

    // Many-to-many navigation property
    public ICollection<Tag> Tags { get; set; } = [];
}
