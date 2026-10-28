using Fakturio.Api.Models;

namespace Fakturio.Api.Dtos;

public record AuditLogEntryResponse(
    Guid Id,
    DateTime OccurredAt,
    AuditAction Action,
    string EntityType,
    Guid? EntityId,
    string? EntityNumber,
    Guid? ActorUserId,
    string? ActorClerkUserId,
    string? ActorDisplayName,
    string? ActorEmail,
    string Summary,
    string? DetailsJson
);

public record AuditLogListQuery(
    int Page = 1,
    int PageSize = 50,
    AuditAction? Action = null,
    Guid? EntityId = null,
    string? Search = null,
    DateTime? From = null,
    DateTime? To = null
);
