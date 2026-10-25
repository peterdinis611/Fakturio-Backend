namespace Fakturio.Api.Models;

public class CompanySettings
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Identity
    public string Name { get; set; } = string.Empty;
    public string? LegalForm { get; set; }
    public string? Ico { get; set; }
    public string? Dic { get; set; }
    public string? IcDph { get; set; }
    public bool IsVatPayer { get; set; } = true;
    public string? CommercialRegister { get; set; }

    // Contact
    public string? Email { get; set; }
    public string? InvoiceEmail { get; set; }
    public string? Phone { get; set; }
    public string? Website { get; set; }
    public string? ContactPerson { get; set; }

    // Address
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string Country { get; set; } = "SK";

    // Banking
    public string? BankIban { get; set; }
    public string? BankBic { get; set; }
    public string? BankName { get; set; }

    // Invoice defaults
    public int DefaultDueDays { get; set; } = 14;
    public decimal DefaultVatRate { get; set; } = 23m;
    public string DefaultCurrency { get; set; } = "EUR";
    public string? InvoicePrefix { get; set; }
    public string? DefaultPaymentMethod { get; set; } = "bank_transfer";
    public string? DefaultConstantSymbol { get; set; }
    public string? DefaultInvoiceNotes { get; set; }
    public string? InvoiceFooter { get; set; }

    // Branding / misc
    public string? LogoUrl { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
