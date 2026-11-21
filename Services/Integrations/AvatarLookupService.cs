using System.Security.Cryptography;
using System.Text;
using Fakturio.Api.Common;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Services.Integrations;

/// <summary>Gravatar + Libravatar URL builder (no remote round-trip).</summary>
public sealed class AvatarLookupService : IAvatarLookupService
{
    public ServiceResult<AvatarLookupResponse> FromEmail(string email)
    {
        var normalized = (email ?? "").Trim().ToLowerInvariant();
        if (normalized.Length < 3 || !normalized.Contains('@', StringComparison.Ordinal))
        {
            return ServiceResult<AvatarLookupResponse>.BadRequest(
                "Valid e-mail is required.",
                ErrorCodes.BadRequest);
        }

        var hash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(normalized)))
            .ToLowerInvariant();
        var gravatar = $"https://www.gravatar.com/avatar/{hash}?d=identicon&s=128";
        var libravatar = $"https://seccdn.libravatar.org/avatar/{hash}?d=identicon&s=128";

        return ServiceResult<AvatarLookupResponse>.Ok(new(
            hash, gravatar, libravatar, "gravatar-libravatar"));
    }
}
