using System.ComponentModel.DataAnnotations;

namespace QuoteHub.Api.Dtos;

/// <summary>
/// What the API returns to clients (safe, read-only contract).
/// </summary>
public record QuoteResponse(
    int Id,
    string Text,
    string Author,
    string Category,
    DateTime CreatedAtUtc,
    IReadOnlyList<string> Tags,
    int LikesCount = 0
);

/// <summary>
/// Standard pagination envelope for list endpoints.
/// </summary>
public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount
)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>
/// What clients send to create a new quote.
/// </summary>
public record CreateQuoteRequest(
    [Required, StringLength(500, MinimumLength = 3)]
    string Text,

    [Required, StringLength(100, MinimumLength = 2)]
    string Author,

    [StringLength(50)]
    string Category = "General",

    IReadOnlyList<string>? Tags = null
);

/// <summary>
/// What clients send to replace an existing quote.
/// </summary>
public record UpdateQuoteRequest(
    [Required, StringLength(500, MinimumLength = 3)]
    string Text,

    [Required, StringLength(100, MinimumLength = 2)]
    string Author,

    [Required, StringLength(50)]
    string Category,

    IReadOnlyList<string>? Tags = null
);

public record LoginRequest(
    [Required] string Username,
    [Required] string Password
);

public record TokenResponse(
    string AccessToken,
    string TokenType,
    DateTime ExpiresAtUtc,
    string Role
);

// Discord Webhook Embed structures (Drop-in compatible with Discord Webhooks API)
public record DiscordEmbedAuthor(string Name);
public record DiscordEmbedFooter(string Text);
public record DiscordEmbed(
    string Title,
    string Description,
    int Color,
    DiscordEmbedAuthor Author,
    DiscordEmbedFooter Footer,
    DateTime Timestamp
);
public record DiscordWebhookPayload(
    string Username,
    IReadOnlyList<DiscordEmbed> Embeds
);
