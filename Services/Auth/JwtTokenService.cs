using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Fakturio.Api.Configuration;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Fakturio.Api.Services.Auth;

public sealed class JwtTokenService(
    IOptions<JwtOptions> options,
    IAdminAccess adminAccess)
{
    public const string SecurityStampClaimType = "sst";
    public const string TokenUseClaimType = "tok";
    public const string HubTokenUse = "hub";

    public (string Token, DateTime ExpiresAt) CreateToken(AppUser user)
    {
        var opts = options.Value;
        var expires = DateTime.UtcNow.AddMinutes(Math.Clamp(opts.ExpireMinutes, 5, 60 * 24 * 30));
        return Issue(user, expires, hubOnly: false);
    }

    /// <summary>Short-lived JWT for SignalR only — rejected on REST API routes.</summary>
    public (string Token, DateTime ExpiresAt) CreateHubToken(AppUser user)
    {
        var opts = options.Value;
        var minutes = Math.Clamp(opts.HubExpireMinutes, 1, 30);
        var expires = DateTime.UtcNow.AddMinutes(minutes);
        return Issue(user, expires, hubOnly: true);
    }

    private (string Token, DateTime ExpiresAt) Issue(AppUser user, DateTime expires, bool hubOnly)
    {
        var opts = options.Value;
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
            new(SecurityStampClaimType, user.SecurityStamp.ToString("N")),
        };

        if (hubOnly)
            claims.Add(new Claim(TokenUseClaimType, HubTokenUse));

        if (!string.IsNullOrWhiteSpace(display))
            claims.Add(new Claim(ClaimTypes.Name, display));

        if (adminAccess.IsAdminEmail(user.Email))
            claims.Add(new Claim(ClaimTypes.Role, AdminAccess.RoleName));

        var token = new JwtSecurityToken(
            issuer: opts.Issuer,
            audience: opts.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
