namespace Fakturio.Api.Configuration;

public sealed class BankingOptions
{
    public const string SectionName = "Banking";

    public Psd2SandboxOptions Psd2Sandbox { get; set; } = new();
}

public sealed class Psd2SandboxOptions
{
    public bool Enabled { get; set; }
}