using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AdminWeb.Contracts.Api;
using AdminWeb.Models;
using Microsoft.IdentityModel.Tokens;

namespace AdminWeb.Services;

public sealed class JwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public AuthDto Create(Tourist tourist)
    {
        var issuer = _configuration["Jwt:Issuer"] ?? "TourGuideSystem.AdminWeb";
        var audience = _configuration["Jwt:Audience"] ?? "TourGuideSystem.UserMobile";
        var signingKey = _configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");
        var expirationDays = Math.Max(1, _configuration.GetValue("Jwt:ExpirationDays", 30));
        var expiresAt = DateTimeOffset.UtcNow.AddDays(expirationDays);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, tourist.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, tourist.Id.ToString()),
            new Claim(ClaimTypes.Email, tourist.Email ?? ""),
            new Claim(ClaimTypes.Name, tourist.FullName ?? tourist.Email ?? $"Tourist #{tourist.Id}")
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new AuthDto
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAt = expiresAt,
            Profile = new TouristProfileDto
            {
                Id = tourist.Id,
                Email = tourist.Email ?? "",
                FullName = tourist.FullName ?? "",
                CreatedAt = tourist.CreatedAt
            }
        };
    }
}
