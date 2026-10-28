namespace Fakturio.Api.Models;

public class ServiceCatalogItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Description { get; set; } = string.Empty;
    public string Unit { get; set; } = "ks";
    public decimal UnitPrice { get; set; }
    public decimal VatRate { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
