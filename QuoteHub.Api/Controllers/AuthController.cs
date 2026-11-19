using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuoteHub.Api.Dtos;
using QuoteHub.Api.Services;

namespace QuoteHub.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController(ITokenService tokenService) : ControllerBase
{
    /// <summary>
    /// Issues a signed JWT Bearer token for Admin or Reader roles.
    /// </summary>
    [HttpPost("token")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<TokenResponse> IssueToken(LoginRequest request)
    {
        var token = tokenService.Authenticate(request);
        return token is null ? Unauthorized() : Ok(token);
    }
}
