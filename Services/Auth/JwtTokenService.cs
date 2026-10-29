using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Fakturio.Api.Configuration;
using Fakturio.Api.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Fakturio.Api.Services.Auth;

public sealed class JwtTokenService(IOptions<JwtOptions> options)
{
    public (string Token, DateTime ExpiresAt) CreateToken(AppUser user)
    {
        var opts = options.Value;
        var expires = DateTime.UtcNow.AddMinutes(Math.Clamp(opts.ExpireMinutes, 5, 60 * 24 * 30));
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opts.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var display = string.Join(
            ' ',
            new[] { user.FirstName, user.LastName }.Where(s => !string.IsNullOrWhiteSpace(s)))
            .Trim();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
        };

        if (!string.IsNullOrWhiteSpace(display))
            claims.Add(new Claim(ClaimTypes.Name, display));

        var token = new JwtSecurityToken(
            issuer: opts.Issuer,
            audience: opts.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
