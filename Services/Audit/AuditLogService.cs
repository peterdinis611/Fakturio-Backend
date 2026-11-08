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
    ICurrentActorAccessor actors,
    ITenantContext tenant) : IAuditLogService
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
        var companyId = await tenant.GetCompanyIdAsync(ct);
        var userId = await tenant.GetUserIdAsync(ct);

        var logs = db.AuditLogs.AsNoTracking()
            .Where(l =>
                l.CompanyId == companyId ||
                (l.CompanyId == null && l.ActorUserId == userId));

        if (query.Action.HasValue)
            logs = logs.Where(l => l.Action == query.Action.Value);

        if (query.EntityId.HasValue && query.EntityId.Value != Guid.Empty)
            logs = logs.Where(l => l.EntityId == query.EntityId.Value);

        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            var type = query.EntityType.Trim();
            logs = logs.Where(l => l.EntityType == type);
        }

        if (query.From.HasValue)
            logs = logs.Where(l => l.OccurredAt >= query.From.Value);

        if (query.To.HasValue)
            logs = logs.Where(l => l.OccurredAt <= query.To.Value);

        if (search is not null)
        {
            logs = logs.Where(l =>
                l.Summary.Contains(search) ||
                (l.EntityNumber != null && l.EntityNumber.Contains(search)) ||
                (l.EntityType != null && l.EntityType.Contains(search)) ||
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

    public Task WriteAsync(
        AuditAction action,
        string entityType,
        Guid? entityId,
        string? entityNumber,
        string summary,
        object? details = null,
        CancellationToken ct = default)
        => WriteCoreAsync(action, entityType, entityId, entityNumber, summary, details, null, ct);

    public Task WriteAsync(
        AuditAction action,
        string entityType,
        Guid? entityId,
        string? entityNumber,
        string summary,
        Guid companyId,
        object? details = null,
        CancellationToken ct = default)
        => WriteCoreAsync(action, entityType, entityId, entityNumber, summary, details, companyId, ct);

    private async Task WriteCoreAsync(
        AuditAction action,
        string entityType,
        Guid? entityId,
        string? entityNumber,
        string summary,
        object? details,
        Guid? companyId,
        CancellationToken ct)
    {
        var actor = await actors.GetAsync(ct);
        var resolvedCompanyId = companyId;
        if (resolvedCompanyId is null || resolvedCompanyId == Guid.Empty)
        {
            try
            {
                resolvedCompanyId = await tenant.GetCompanyIdAsync(ct);
            }
            catch (UnauthorizedAccessException)
            {
                resolvedCompanyId = null;
            }
        }

        var entry = new AuditLogEntry
        {
            CompanyId = resolvedCompanyId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            EntityNumber = entityNumber,
            ActorUserId = actor.UserId,
            ActorClerkUserId = actor.Subject,
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
        e.DetailsJson,
        e.CompanyId);
}
