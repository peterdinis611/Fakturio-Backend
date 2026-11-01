using Fakturio.Api.Models;

namespace Fakturio.Api.Dtos;

public record NotificationResponse(
    Guid Id,
    NotificationKind Kind,
    string Title,
    string Body,
    string? Href,
    Guid? InvoiceId,
    string? InvoiceNumber,
    bool IsRead,
    DateTime CreatedAt,
    DateTime? ReadAt
);

public record NotificationListResponse(
    IReadOnlyList<NotificationResponse> Items,
    int UnreadCount
);

public record NotificationPreferencesResponse(
    bool RemindersEnabled,
    bool InAppEnabled,
    bool EmailEnabled,
    int WithinDays,
    bool OnlyOverdue,
    int? QuietHoursStartUtc,
    int? QuietHoursEndUtc,
    DateTime UpdatedAt
);

public record UpdateNotificationPreferencesRequest(
    bool? RemindersEnabled = null,
    bool? InAppEnabled = null,
    bool? EmailEnabled = null,
    int? WithinDays = null,
    bool? OnlyOverdue = null,
    int? QuietHoursStartUtc = null,
    int? QuietHoursEndUtc = null,
    bool ClearQuietHours = false
);
