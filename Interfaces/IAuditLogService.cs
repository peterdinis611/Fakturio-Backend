using Fakturio.Api.Dtos;
using Fakturio.Api.Models;

namespace Fakturio.Api.Interfaces;

public interface IAuditLogService
{
    Task<PagedResponse<AuditLogEntryResponse>> ListAsync(
        AuditLogListQuery query,
        CancellationToken ct = default);

    Task WriteAsync(
        AuditAction action,
        string entityType,
        Guid? entityId,
        string? entityNumber,
        string summary,
        object? details = null,
        CancellationToken ct = default);
}
