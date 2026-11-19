using Microsoft.AspNetCore.Mvc;
using QuoteHub.Api.Dtos;
using QuoteHub.Api.Services;

namespace QuoteHub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QuotesController(IQuoteService quoteService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<QuoteResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<QuoteResponse>>> GetAll(
        [FromQuery] string? category = null,
        CancellationToken ct = default)
    {
        return Ok(await quoteService.GetAllAsync(category, ct));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(QuoteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QuoteResponse>> GetById(int id, CancellationToken ct)
    {
        var quote = await quoteService.GetByIdAsync(id, ct);
        return quote is null ? NotFound() : Ok(quote);
    }

    [HttpGet("random")]
    [ProducesResponseType(typeof(QuoteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QuoteResponse>> GetRandom(CancellationToken ct)
    {
        var quote = await quoteService.GetRandomAsync(ct);
        return quote is null ? NotFound() : Ok(quote);
    }

    [HttpGet("search")]
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
    [ProducesResponseType(typeof(QuoteResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<QuoteResponse>> Create(CreateQuoteRequest request, CancellationToken ct)
    {
        var created = await quoteService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(QuoteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QuoteResponse>> Update(int id, UpdateQuoteRequest request, CancellationToken ct)
    {
        var updated = await quoteService.UpdateAsync(id, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        return await quoteService.DeleteAsync(id, ct) ? NoContent() : NotFound();
    }
}
