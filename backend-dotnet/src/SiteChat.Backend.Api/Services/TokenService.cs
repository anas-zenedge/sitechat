using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SiteChat.Backend.Api.Configuration;
using SiteChat.Backend.Api.Models;

namespace SiteChat.Backend.Api.Services;

/// <summary>
/// Creates JWT bearer tokens for authenticated SiteChat users.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Creates an access token for a user.
    /// </summary>
    /// <param name="user">The authenticated user.</param>
    /// <returns>A signed bearer token.</returns>
    string CreateAccessToken(MongoUser user);
}

/// <summary>
/// Implements HS256 JWT token creation.
/// </summary>
public sealed class JwtTokenService(IOptions<SiteChatOptions> options) : ITokenService
{
    private readonly SiteChatOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc />
    public string CreateAccessToken(MongoUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var userId = string.IsNullOrWhiteSpace(user.UserId) ? user.ObjectId.ToString() : user.UserId;
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Jwt.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(_options.Jwt.ExpireHours),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
