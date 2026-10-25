namespace Fakturio.Api.Models;

public class CompanySettings
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Ico { get; set; }
    public string? Dic { get; set; }
    public string? IcDph { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string Country { get; set; } = "SK";
    public string? BankIban { get; set; }
    public string? BankBic { get; set; }
    public string? BankName { get; set; }
    public int DefaultDueDays { get; set; } = 14;
    public decimal DefaultVatRate { get; set; } = 23m;
    public string DefaultCurrency { get; set; } = "EUR";
    public string? InvoicePrefix { get; set; }
    public string? Notes { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
