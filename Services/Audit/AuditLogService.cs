using System.Text.Json;
using Fakturio.Api.Data;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Models;
using Fakturio.Api.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Services.Audit;

public sealed class AuditLogService(
    AppDbContext db,
    ICurrentActorAccessor actors) : IAuditLogService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public async Task<PagedResponse<AuditLogEntryResponse>> ListAsync(
        AuditLogListQuery query,
        CancellationToken ct = default)
    {
        var (page, pageSize) = ServiceGuards.NormalizePaging(query.Page, query.PageSize);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var search = ServiceGuards.NormalizeOptionalSearch(query.Search);

        var logs = db.AuditLogs.AsNoTracking().AsQueryable();

        if (query.Action.HasValue)
            logs = logs.Where(l => l.Action == query.Action.Value);

        if (query.EntityId.HasValue && query.EntityId.Value != Guid.Empty)
            logs = logs.Where(l => l.EntityId == query.EntityId.Value);

        if (query.From.HasValue)
            logs = logs.Where(l => l.OccurredAt >= query.From.Value);

        if (query.To.HasValue)
            logs = logs.Where(l => l.OccurredAt <= query.To.Value);

        if (search is not null)
        {
            logs = logs.Where(l =>
                l.Summary.Contains(search) ||
                (l.EntityNumber != null && l.EntityNumber.Contains(search)) ||
                (l.ActorDisplayName != null && l.ActorDisplayName.Contains(search)) ||
                (l.ActorEmail != null && l.ActorEmail.Contains(search)));
        }

        var totalCount = await logs.CountAsync(ct);
        var items = await logs
            .OrderByDescending(l => l.OccurredAt)
            .ThenByDescending(l => l.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResponse<AuditLogEntryResponse>(
            items.Select(ToResponse).ToList(),
            page,
            pageSize,
            totalCount,
            (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public async Task WriteAsync(
        AuditAction action,
        string entityType,
        Guid? entityId,
        string? entityNumber,
        string summary,
        object? details = null,
        CancellationToken ct = default)
    {
        var actor = await actors.GetAsync(ct);
        var entry = new AuditLogEntry
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            EntityNumber = entityNumber,
            ActorUserId = actor.UserId,
            ActorClerkUserId = actor.ClerkUserId,
            ActorDisplayName = actor.DisplayName,
            ActorEmail = actor.Email,
            Summary = summary.Trim(),
            DetailsJson = details is null
                ? null
                : JsonSerializer.Serialize(details, JsonOptions),
            OccurredAt = DateTime.UtcNow
        };

        db.AuditLogs.Add(entry);
        await db.SaveChangesAsync(ct);
    }

    private static AuditLogEntryResponse ToResponse(AuditLogEntry e) => new(
        e.Id,
        e.OccurredAt,
        e.Action,
        e.EntityType,
        e.EntityId,
        e.EntityNumber,
        e.ActorUserId,
        e.ActorClerkUserId,
        e.ActorDisplayName,
        e.ActorEmail,
        e.Summary,
        e.DetailsJson);
}
