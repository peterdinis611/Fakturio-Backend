namespace Fakturio.Api.Models;

public enum RecurrenceInterval
{
    Monthly = 0,
    Quarterly = 1,
    Yearly = 2,
}

/// <summary>Template that generates draft invoices on a schedule.</summary>
public class RecurringInvoice : ICompanyOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }

    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public RecurrenceInterval Interval { get; set; } = RecurrenceInterval.Monthly;
    public int DayOfMonth { get; set; } = 1;
    public int DueDays { get; set; } = 14;
    public bool IsActive { get; set; } = true;

    public DateOnly NextRunOn { get; set; }
    public DateOnly? LastGeneratedOn { get; set; }
    public Guid? LastInvoiceId { get; set; }

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<RecurringInvoiceItem> Items { get; set; } = [];
}

public class RecurringInvoiceItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecurringInvoiceId { get; set; }
    public RecurringInvoice RecurringInvoice { get; set; } = null!;

    public Guid? ServiceCatalogItemId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Unit { get; set; } = "ks";
    public decimal Quantity { get; set; } = 1m;
    public decimal UnitPrice { get; set; }
    public decimal VatRate { get; set; }
    public int SortOrder { get; set; }
}
