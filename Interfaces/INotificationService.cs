using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface INotificationService
{
    Task<NotificationListResponse> ListAsync(int take = 30, CancellationToken ct = default);
    Task<int> UnreadCountAsync(CancellationToken ct = default);
    Task<ServiceResult> MarkReadAsync(Guid id, CancellationToken ct = default);
    Task<ServiceResult> MarkAllReadAsync(CancellationToken ct = default);
    Task<NotificationPreferencesResponse> GetPreferencesAsync(CancellationToken ct = default);
    Task<NotificationPreferencesResponse> UpdatePreferencesAsync(
        UpdateNotificationPreferencesRequest request,
        CancellationToken ct = default);

    /// <summary>Creates an in-app notification and pushes it over SignalR when enabled.</summary>
    Task<NotificationResponse?> PublishAsync(
        Guid userId,
        Guid companyId,
        string title,
        string body,
        string? href,
        Guid? invoiceId,
        string? invoiceNumber,
        CancellationToken ct = default);

    /// <summary>Pushes a sample notification to the current user (bypasses quiet hours).</summary>
    Task<NotificationResponse> PublishTestAsync(CancellationToken ct = default);
}
