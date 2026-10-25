namespace Fakturio.Api.Models;

public class InvoiceItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;

    public string Description { get; set; } = string.Empty;
    public string Unit { get; set; } = "ks";
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatRate { get; set; } = 23m;
    public int SortOrder { get; set; }

    public decimal Subtotal => Math.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);
    public decimal VatAmount => Math.Round(Subtotal * VatRate / 100m, 2, MidpointRounding.AwayFromZero);
    public decimal Total => Subtotal + VatAmount;
}
