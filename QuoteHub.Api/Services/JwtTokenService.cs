using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using QuoteHub.Api.Dtos;

namespace QuoteHub.Api.Services;

public class JwtTokenService(IConfiguration configuration) : ITokenService
{
    public TokenResponse? Authenticate(LoginRequest request)
    {
        var adminUser = configuration["Auth:AdminUsername"] ?? "admin";
        var adminPass = configuration["Auth:AdminPassword"] ?? "QuoteHubAdmin!2026";
        var readerUser = configuration["Auth:ReaderUsername"] ?? "reader";
        var readerPass = configuration["Auth:ReaderPassword"] ?? "QuoteHubReader!2026";

        string? role = null;
        if (string.Equals(request.Username, adminUser, StringComparison.Ordinal) &&
            string.Equals(request.Password, adminPass, StringComparison.Ordinal))
        {
            role = "Admin";
        }
        else if (string.Equals(request.Username, readerUser, StringComparison.Ordinal) &&
                 string.Equals(request.Password, readerPass, StringComparison.Ordinal))
        {
            role = "Reader";
        }

        if (role is null)
        {
            return null;
        }

        var jwtKey = configuration["Jwt:Key"]
            ?? "QuoteHubDotNet-SuperSecret-DevKey-ChangeInProduction-2026!";
        var issuer = configuration["Jwt:Issuer"] ?? "QuoteHub.Api";
        var audience = configuration["Jwt:Audience"] ?? "QuoteHub.Clients";
        var expiresAtUtc = DateTime.UtcNow.AddHours(1);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, request.Username),
            new Claim(ClaimTypes.Name, request.Username),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        var encoded = new JwtSecurityTokenHandler().WriteToken(token);
        return new TokenResponse(encoded, "Bearer", expiresAtUtc, role);
    }
}
