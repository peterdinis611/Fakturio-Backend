namespace Fakturio.Api.Models;

/// <summary>Visual layout for generated invoice PDFs (company-level choice).</summary>
public static class InvoicePdfTemplates
{
    public const string Classic = "classic";
    public const string Modern = "modern";
    public const string Compact = "compact";

    public static readonly string[] All = [Classic, Modern, Compact];

    public static string Normalize(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            Modern => Modern,
            Compact => Compact,
            _ => Classic,
        };
}
