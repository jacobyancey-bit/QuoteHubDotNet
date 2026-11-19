using QuoteHub.Api.Dtos;

namespace QuoteHub.Api.Services;

public interface IQuoteService
{
    IReadOnlyList<QuoteResponse> GetAll(string? category = null);
    QuoteResponse? GetById(int id);
    QuoteResponse? GetRandom();
    IReadOnlyList<QuoteResponse> Search(string query);
    QuoteResponse Create(CreateQuoteRequest request);
    QuoteResponse? Update(int id, UpdateQuoteRequest request);
    bool Delete(int id);
}
