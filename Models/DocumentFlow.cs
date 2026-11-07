namespace Fakturio.Api.Models;

/// <summary>
/// Issued = výnosy / vystavené doklady (Faktúry).
/// Received = náklady / prijaté doklady (Náklady).
/// </summary>
public enum DocumentFlow
{
    Issued = 0,
    Received = 1,
}
