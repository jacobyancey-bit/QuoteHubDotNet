using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using QuoteHub.Api.Dtos;
using QuoteHub.Api.Services;

namespace QuoteHub.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Route("api/[controller]")]
public class QuotesController(IQuoteService quoteService, IOutputCacheStore cacheStore) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [OutputCache(PolicyName = "QuotesList")]
    [ProducesResponseType(typeof(PagedResult<QuoteResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<QuoteResponse>>> GetAll(
        [FromQuery] string? category = null,
        [FromQuery] string? tag = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        return Ok(await quoteService.GetAllAsync(category, tag, page, pageSize, ct));
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(QuoteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QuoteResponse>> GetById(int id, CancellationToken ct)
    {
        var quote = await quoteService.GetByIdAsync(id, ct);
        return quote is null ? NotFound() : Ok(quote);
    }

    [HttpGet("random")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(QuoteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QuoteResponse>> GetRandom(CancellationToken ct)
    {
        var quote = await quoteService.GetRandomAsync(ct);
        return quote is null ? NotFound() : Ok(quote);
    }

    [HttpGet("daily")]
    [AllowAnonymous]
    [OutputCache(Duration = 3600)]
    [ProducesResponseType(typeof(QuoteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QuoteResponse>> GetDaily(CancellationToken ct)
    {
        var quote = await quoteService.GetDailyQuoteAsync(ct);
        return quote is null ? NotFound() : Ok(quote);
    }

    /// <summary>
    /// Returns the top-voted quotes for popular leaderboards.
    /// </summary>
    [HttpGet("top")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<QuoteResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<QuoteResponse>>> GetTop(
        [FromQuery] int limit = 10,
        CancellationToken ct = default)
    {
        return Ok(await quoteService.GetTopQuotesAsync(limit, ct));
    }

    /// <summary>
    /// Upvotes / likes a quote. Publicly accessible.
    /// </summary>
    [HttpPost("{id:int}/like")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Like(int id, CancellationToken ct)
    {
        var newLikes = await quoteService.LikeQuoteAsync(id, ct);
        if (newLikes is null)
        {
            return NotFound();
        }

        await cacheStore.EvictByTagAsync("quotes", ct);
        return Ok(new { id, likes = newLikes });
    }

    /// <summary>
    /// Formats a quote into a ready-to-post Discord Webhook Rich Embed.
    /// Perfect for Discord and Slack bot creators.
    /// </summary>
    [HttpGet("{id:int}/discord")]
    [HttpGet("daily/discord")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(DiscordWebhookPayload), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DiscordWebhookPayload>> GetDiscordEmbed(int? id, CancellationToken ct)
    {
        var quote = id.HasValue
            ? await quoteService.GetByIdAsync(id.Value, ct)
            : await quoteService.GetDailyQuoteAsync(ct);

        if (quote is null)
        {
            return NotFound();
        }

        var tagsText = quote.Tags.Count > 0 ? " • #" + string.Join(" #", quote.Tags) : "";
        var embed = new DiscordEmbed(
            Title: $"{quote.Category} Quote of the Day",
            Description: $"*{quote.Text}*\n\n**— {quote.Author}**",
            Color: 0x38bdf8, // Sky Blue
            Author: new DiscordEmbedAuthor("QuoteHub Bot"),
            Footer: new DiscordEmbedFooter($"QuoteHub API{tagsText}"),
            Timestamp: quote.CreatedAtUtc
        );

        return Ok(new DiscordWebhookPayload("QuoteHub Bot", [embed]));
    }

    /// <summary>
    /// Generates a dynamic SVG quote card vector image for social embeds, Discord, and websites.
    /// </summary>
    [HttpGet("{id:int}/card.svg")]
    [HttpGet("daily/card.svg")]
    [AllowAnonymous]
    [Produces("image/svg+xml")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCardSvg(int? id, [FromQuery] string theme = "dark", CancellationToken ct = default)
    {
        var quote = id.HasValue
            ? await quoteService.GetByIdAsync(id.Value, ct)
            : await quoteService.GetDailyQuoteAsync(ct);

        if (quote is null)
        {
            return NotFound();
        }

        var svg = CardGenerator.GenerateSvg(quote, theme);
        return Content(svg, "image/svg+xml; charset=utf-8");
    }

    [HttpGet("search")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<QuoteResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<QuoteResponse>>> Search(
        [FromQuery] string? q,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest("Query parameter 'q' is required.");
        }

        return Ok(await quoteService.SearchAsync(q, ct));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(QuoteResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<QuoteResponse>> Create(CreateQuoteRequest request, CancellationToken ct)
    {
        var created = await quoteService.CreateAsync(request, ct);
        await cacheStore.EvictByTagAsync("quotes", ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(QuoteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QuoteResponse>> Update(int id, UpdateQuoteRequest request, CancellationToken ct)
    {
        var updated = await quoteService.UpdateAsync(id, request, ct);
        if (updated is null)
        {
            return NotFound();
        }

        await cacheStore.EvictByTagAsync("quotes", ct);
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var deleted = await quoteService.DeleteAsync(id, ct);
        if (!deleted)
        {
            return NotFound();
        }

        await cacheStore.EvictByTagAsync("quotes", ct);
        return NoContent();
    }
}
