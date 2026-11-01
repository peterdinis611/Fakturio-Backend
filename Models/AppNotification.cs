namespace Fakturio.Api.Models;

public enum NotificationKind
{
    Reminder = 0,
    System = 1,
}

public class AppNotification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid CompanyId { get; set; }
    public NotificationKind Kind { get; set; } = NotificationKind.Reminder;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Href { get; set; }
    public Guid? InvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}

/// <summary>Per-user notification channel preferences.</summary>
public class NotificationPreferences
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    /// <summary>Master switch for reminder notifications.</summary>
    public bool RemindersEnabled { get; set; } = true;

    /// <summary>Push into in-app inbox + SignalR.</summary>
    public bool InAppEnabled { get; set; } = true;

    /// <summary>Reserved for SMTP — when false, email channel is skipped.</summary>
    public bool EmailEnabled { get; set; }

    /// <summary>How many days before due date to notify (0–90).</summary>
    public int WithinDays { get; set; } = 7;

    /// <summary>Only notify overdue invoices.</summary>
    public bool OnlyOverdue { get; set; }

    /// <summary>Quiet hours start (UTC hour, inclusive). Null = no quiet hours.</summary>
    public int? QuietHoursStartUtc { get; set; }

    /// <summary>Quiet hours end (UTC hour, exclusive).</summary>
    public int? QuietHoursEndUtc { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
