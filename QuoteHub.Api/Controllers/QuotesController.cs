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
    public ActionResult<IReadOnlyList<QuoteResponse>> GetAll([FromQuery] string? category = null)
    {
        return Ok(quoteService.GetAll(category));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(QuoteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<QuoteResponse> GetById(int id)
    {
        var quote = quoteService.GetById(id);
        return quote is null ? NotFound() : Ok(quote);
    }

    [HttpGet("random")]
    [ProducesResponseType(typeof(QuoteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<QuoteResponse> GetRandom()
    {
        var quote = quoteService.GetRandom();
        return quote is null ? NotFound() : Ok(quote);
    }

    [HttpPost]
    [ProducesResponseType(typeof(QuoteResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<QuoteResponse> Create(CreateQuoteRequest request)
    {
        var created = quoteService.Create(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(QuoteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<QuoteResponse> Update(int id, UpdateQuoteRequest request)
    {
        var updated = quoteService.Update(id, request);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Delete(int id)
    {
        return quoteService.Delete(id) ? NoContent() : NotFound();
    }
    [HttpGet("search")]
    [ProducesResponseType(typeof(IReadOnlyList<QuoteResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<IReadOnlyList<QuoteResponse>> Search([FromQuery] string? q)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest("Query parameter 'q' is required.");
        }

        return Ok(quoteService.Search(q));
    }
}
