namespace Fakturio.Api.Models;

public class AuditLogEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public AuditAction Action { get; set; }
    public string EntityType { get; set; } = "Invoice";
    public Guid? EntityId { get; set; }
    public string? EntityNumber { get; set; }

    public Guid? ActorUserId { get; set; }
    public string? ActorClerkUserId { get; set; }
    public string? ActorDisplayName { get; set; }
    public string? ActorEmail { get; set; }

    /// <summary>Short human-readable summary (SK).</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Optional JSON details (reason, paidAt, related id, …).</summary>
    public string? DetailsJson { get; set; }
}
