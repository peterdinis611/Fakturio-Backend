namespace Fakturio.Api.Models;

public class Invoice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Number { get; set; } = string.Empty;
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    // Snapshot of customer at issue time (immutable history)
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerIco { get; set; }
    public string? CustomerDic { get; set; }
    public string? CustomerIcDph { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerStreet { get; set; }
    public string? CustomerCity { get; set; }
    public string? CustomerPostalCode { get; set; }
    public string CustomerCountry { get; set; } = "SK";

    // Snapshot of seller at issue time
    public string? SellerName { get; set; }
    public string? SellerIco { get; set; }
    public string? SellerDic { get; set; }
    public string? SellerIcDph { get; set; }
    public string? SellerStreet { get; set; }
    public string? SellerCity { get; set; }
    public string? SellerPostalCode { get; set; }
    public string? SellerCountry { get; set; }

    public DateOnly IssueDate { get; set; }
    public DateOnly DueDate { get; set; }
    public DateOnly? DeliveryDate { get; set; }
    public DateOnly? PaidAt { get; set; }

    public string? CancelReason { get; set; }
    public DateOnly? CancelledAt { get; set; }

    public DateTime? LastReminderSentAt { get; set; }
    public int ReminderCount { get; set; }

    public string Currency { get; set; } = "EUR";
    public string? VariableSymbol { get; set; }
    public string? ConstantSymbol { get; set; }
    public string? SpecificSymbol { get; set; }
    public string? PaymentMethod { get; set; } = "bank_transfer";
    public string? BankIban { get; set; }
    public string? BankBic { get; set; }
    public string? Notes { get; set; }

    public decimal Subtotal { get; set; }
    public decimal VatTotal { get; set; }
    public decimal Total { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<InvoiceItem> Items { get; set; } = [];
}
