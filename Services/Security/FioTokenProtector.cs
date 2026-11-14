using Microsoft.AspNetCore.DataProtection;

namespace Fakturio.Api.Services.Security;

public interface IFioTokenProtector
{
    string? Protect(string? plaintext);
    string? Unprotect(string? stored);
    bool LooksProtected(string? stored);
}

public sealed class FioTokenProtector(IDataProtectionProvider provider) : IFioTokenProtector
{
    public const string ProtectorPurpose = "Fakturio.FioBankToken.v1";
    private const string Prefix = "dp1:";

    private readonly IDataProtector _protector = provider.CreateProtector(ProtectorPurpose);

    public string? Protect(string? plaintext)
    {
        if (string.IsNullOrWhiteSpace(plaintext))
            return null;

        var trimmed = plaintext.Trim();
        if (LooksProtected(trimmed))
            return trimmed;

        return Prefix + _protector.Protect(trimmed);
    }

    public string? Unprotect(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
            return null;

        var value = stored.Trim();
        if (!LooksProtected(value))
            return value;

        try
        {
            return _protector.Unprotect(value[Prefix.Length..]);
        }
        catch
        {
            return null;
        }
    }

    public bool LooksProtected(string? stored) =>
        !string.IsNullOrWhiteSpace(stored)
        && stored.StartsWith(Prefix, StringComparison.Ordinal);
}
