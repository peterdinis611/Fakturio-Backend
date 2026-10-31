using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fakturio.Api.Caching;
using Fakturio.Api.Common;
using Fakturio.Api.Data;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Models;
using Fakturio.Api.Services.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Services.Gdpr;

public sealed class GdprService(
    AppDbContext db,
    ITenantContext tenant,
    IPasswordHasher<AppUser> passwordHasher,
    ILogoStorageService logos,
    ICacheInvalidator cache) : IGdprService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public async Task<ServiceResult<ExportFile>> ExportMyDataAsync(CancellationToken ct = default)
    {
        var (userId, companyId) = await tenant.GetAsync(ct);

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return ServiceResult<ExportFile>.NotFound("User not found.", ErrorCodes.NotFound);

        var company = await db.CompanySettings.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == companyId, ct);
        var customers = await db.Customers.AsNoTracking()
            .Where(c => c.CompanyId == companyId)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
        var invoices = await db.Invoices.AsNoTracking()
            .Include(i => i.Items)
            .Where(i => i.CompanyId == companyId)
            .OrderBy(i => i.IssueDate)
            .ToListAsync(ct);
        var catalog = await db.ServiceCatalogItems.AsNoTracking()
            .Where(s => s.CompanyId == companyId)
            .OrderBy(s => s.SortOrder)
            .ToListAsync(ct);
        var notes = await db.Notes.AsNoTracking()
            .Where(n => n.CompanyId == companyId)
            .OrderByDescending(n => n.UpdatedAt)
            .ToListAsync(ct);
        var recurring = await db.RecurringInvoices.AsNoTracking()
            .Include(r => r.Items)
            .Where(r => r.CompanyId == companyId)
            .OrderBy(r => r.Name)
            .ToListAsync(ct);
        var audit = await db.AuditLogs.AsNoTracking()
            .Where(a => a.ActorUserId == userId)
            .OrderByDescending(a => a.OccurredAt)
            .Take(5_000)
            .ToListAsync(ct);

        var payload = new
        {
            exportedAt = DateTime.UtcNow,
            format = "fakturio-gdpr-export-v1",
            user = new
            {
                user.Id,
                user.Email,
                user.FirstName,
                user.LastName,
                user.CreatedAt,
                user.UpdatedAt,
                user.LastSeenAt
            },
            company,
            customers,
            invoices = invoices.Select(i => new
            {
                i.Id,
                i.Number,
                i.DocumentType,
                i.Status,
                i.CustomerId,
                i.CustomerName,
                i.CustomerIco,
                i.CustomerEmail,
                i.IssueDate,
                i.DueDate,
                i.DeliveryDate,
                i.PaidAt,
                i.Currency,
                i.Subtotal,
                i.VatTotal,
                i.Total,
                i.VariableSymbol,
                i.Notes,
                i.CreatedAt,
                items = i.Items.OrderBy(x => x.SortOrder).Select(x => new
                {
                    x.Description,
                    x.Unit,
                    x.Quantity,
                    x.UnitPrice,
                    x.VatRate,
                    x.Subtotal,
                    x.VatAmount,
                    x.Total
                })
            }),
            serviceCatalog = catalog,
            notes,
            recurringInvoices = recurring.Select(r => new
            {
                r.Id,
                r.Name,
                r.CustomerId,
                r.Interval,
                r.DayOfMonth,
                r.DueDays,
                r.IsActive,
                r.NextRunOn,
                r.LastGeneratedOn,
                r.Notes,
                items = r.Items.OrderBy(i => i.SortOrder)
            }),
            auditLog = audit.Select(a => new
            {
                a.OccurredAt,
                a.Action,
                a.EntityType,
                a.EntityId,
                a.EntityNumber,
                a.Summary,
                a.DetailsJson
            })
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var readme = Encoding.UTF8.GetBytes(
            """
            Fakturio — export osobných a firemných údajov (GDPR)
            ====================================================

            Súbor data.json obsahuje účet, firmu, odberateľov, faktúry, katalóg,
            poznámky, opakované faktúry a audit log (max. 5000 záznamov).

            Heslá a session tokeny nie sú súčasťou exportu.
            """);

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(zip, "README.txt", readme);
            WriteEntry(zip, "data.json", Encoding.UTF8.GetBytes(json));
        }

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        return ServiceResult<ExportFile>.Ok(new ExportFile(
            ms.ToArray(),
            $"fakturio-gdpr-{stamp}.zip",
            "application/zip"));
    }

    public async Task<ServiceResult> DeleteMyAccountAsync(
        DeleteAccountRequest request,
        CancellationToken ct = default)
    {
        var (userId, companyId) = await tenant.GetAsync(ct);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return ServiceResult.NotFound("User not found.", ErrorCodes.NotFound);

        var verify = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verify == PasswordVerificationResult.Failed)
            return ServiceResult.Unauthorized("Neplatné heslo.", ErrorCodes.Unauthorized);

        var company = await db.CompanySettings.FirstOrDefaultAsync(c => c.Id == companyId, ct);
        var logoUrl = company?.LogoUrl;

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var invoiceIds = await db.Invoices
            .Where(i => i.CompanyId == companyId)
            .Select(i => i.Id)
            .ToListAsync(ct);

        if (invoiceIds.Count > 0)
        {
            await db.InvoiceItems
                .Where(ii => invoiceIds.Contains(ii.InvoiceId))
                .ExecuteDeleteAsync(ct);
            await db.Invoices
                .Where(i => i.CompanyId == companyId)
                .ExecuteDeleteAsync(ct);
        }

        var recurringIds = await db.RecurringInvoices
            .Where(r => r.CompanyId == companyId)
            .Select(r => r.Id)
            .ToListAsync(ct);
        if (recurringIds.Count > 0)
        {
            await db.RecurringInvoiceItems
                .Where(i => recurringIds.Contains(i.RecurringInvoiceId))
                .ExecuteDeleteAsync(ct);
            await db.RecurringInvoices
                .Where(r => r.CompanyId == companyId)
                .ExecuteDeleteAsync(ct);
        }

        await db.ServiceCatalogItems.Where(s => s.CompanyId == companyId).ExecuteDeleteAsync(ct);
        await db.Notes.Where(n => n.CompanyId == companyId).ExecuteDeleteAsync(ct);
        await db.Customers.Where(c => c.CompanyId == companyId).ExecuteDeleteAsync(ct);
        await db.AuditLogs.Where(a => a.ActorUserId == userId).ExecuteDeleteAsync(ct);

        if (company is not null)
            db.CompanySettings.Remove(company);

        db.Users.Remove(user);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await cache.InvalidateAllAsync(ct);
        await logos.DeleteIfLocalAsync(logoUrl, ct);

        return ServiceResult.Ok(StatusCodes.Status204NoContent);
    }

    private static void WriteEntry(ZipArchive zip, string name, byte[] content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(content);
    }
}
