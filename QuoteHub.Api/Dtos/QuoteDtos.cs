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
    DateTime CreatedAtUtc
);

/// <summary>
/// What clients send to create a new quote.
/// </summary>
public record CreateQuoteRequest(
    [Required, StringLength(500, MinimumLength = 3)]
    string Text,

    [Required, StringLength(100, MinimumLength = 2)]
    string Author,

    [StringLength(50)]
    string Category = "General"
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
    string Category
);
