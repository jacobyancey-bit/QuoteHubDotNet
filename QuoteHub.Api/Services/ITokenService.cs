using QuoteHub.Api.Dtos;

namespace QuoteHub.Api.Services;

public interface ITokenService
{
    TokenResponse? Authenticate(LoginRequest request);
}
