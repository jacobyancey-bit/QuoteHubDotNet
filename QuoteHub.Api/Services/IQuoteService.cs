using QuoteHub.Api.Dtos;

namespace QuoteHub.Api.Services;

public interface IQuoteService
{
    Task<IReadOnlyList<QuoteResponse>> GetAllAsync(string? category = null, CancellationToken ct = default);
    Task<QuoteResponse?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<QuoteResponse?> GetRandomAsync(CancellationToken ct = default);
    Task<IReadOnlyList<QuoteResponse>> SearchAsync(string query, CancellationToken ct = default);
    Task<QuoteResponse> CreateAsync(CreateQuoteRequest request, CancellationToken ct = default);
    Task<QuoteResponse?> UpdateAsync(int id, UpdateQuoteRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}
