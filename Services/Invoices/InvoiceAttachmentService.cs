using Fakturio.Api.Common;
using Fakturio.Api.Configuration;
using Fakturio.Api.Data;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Models;
using Fakturio.Api.Services.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Fakturio.Api.Services.Invoices;

public sealed class InvoiceAttachmentService(
    AppDbContext db,
    ITenantContext tenant,
    IWebHostEnvironment env,
    IOptions<AttachmentStorageOptions> options,
    ILogger<InvoiceAttachmentService> logger) : IInvoiceAttachmentService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp", ".pdf", ".heic", ".heif",
    };

    public async Task<ServiceResult<IReadOnlyList<InvoiceAttachmentResponse>>> ListAsync(
        Guid invoiceId,
        CancellationToken ct = default)
    {
        var companyId = await tenant.GetCompanyIdAsync(ct);
        var exists = await db.Invoices.AsNoTracking()
            .AnyAsync(i => i.Id == invoiceId && i.CompanyId == companyId, ct);
        if (!exists)
            return ServiceResult<IReadOnlyList<InvoiceAttachmentResponse>>.NotFound(
                "Invoice not found.",
                ErrorCodes.InvoiceNotFound);

        var items = await db.InvoiceAttachments.AsNoTracking()
            .Where(a => a.InvoiceId == invoiceId && a.CompanyId == companyId)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new InvoiceAttachmentResponse(
                a.Id, a.InvoiceId, a.FileName, a.ContentType, a.SizeBytes, a.CreatedAt))
            .ToListAsync(ct);

        return ServiceResult<IReadOnlyList<InvoiceAttachmentResponse>>.Ok(items);
    }

    public async Task<ServiceResult<InvoiceAttachmentResponse>> UploadAsync(
        Guid invoiceId,
        IFormFile file,
        CancellationToken ct = default)
    {
        var opts = options.Value;
        var companyId = await tenant.GetCompanyIdAsync(ct);
        var invoice = await db.Invoices.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.CompanyId == companyId, ct);
        if (invoice is null)
            return ServiceResult<InvoiceAttachmentResponse>.NotFound(
                "Invoice not found.",
                ErrorCodes.InvoiceNotFound);

        if (file.Length <= 0)
            return ServiceResult<InvoiceAttachmentResponse>.BadRequest("Empty file.", ErrorCodes.BadRequest);

        if (file.Length > opts.MaxBytes)
            return ServiceResult<InvoiceAttachmentResponse>.BadRequest(
                $"File must be at most {opts.MaxBytes / (1024 * 1024)} MB.",
                ErrorCodes.FileTooLarge);

        var contentType = (file.ContentType ?? string.Empty).Trim().ToLowerInvariant();
        if (!opts.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            return ServiceResult<InvoiceAttachmentResponse>.BadRequest(
                "Allowed formats: PNG, JPEG, WebP, HEIC, PDF.",
                ErrorCodes.InvalidFileType);

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
            ext = ExtensionForContentType(contentType) ?? ".bin";

        var dir = GetInvoiceDirectory(companyId, invoiceId);
        Directory.CreateDirectory(dir);

        var storedName = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        var absolutePath = Path.Combine(dir, storedName);
        await using (var stream = File.Create(absolutePath))
            await file.CopyToAsync(stream, ct);

        var relative = Path.Combine("attachments", companyId.ToString("N"), invoiceId.ToString("N"), storedName)
            .Replace('\\', '/');

        var entity = new InvoiceAttachment
        {
            CompanyId = companyId,
            InvoiceId = invoiceId,
            FileName = Path.GetFileName(file.FileName),
            ContentType = contentType,
            SizeBytes = file.Length,
            StoragePath = relative,
        };
        db.InvoiceAttachments.Add(entity);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Saved attachment {Id} for invoice {InvoiceId}", entity.Id, invoiceId);
        return ServiceResult<InvoiceAttachmentResponse>.Ok(new InvoiceAttachmentResponse(
            entity.Id, entity.InvoiceId, entity.FileName, entity.ContentType, entity.SizeBytes, entity.CreatedAt));
    }

    public async Task<ServiceResult<InvoicePdfFile>> DownloadAsync(
        Guid attachmentId,
        CancellationToken ct = default)
    {
        var companyId = await tenant.GetCompanyIdAsync(ct);
        var attachment = await db.InvoiceAttachments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.CompanyId == companyId, ct);
        if (attachment is null)
            return ServiceResult<InvoicePdfFile>.NotFound("Attachment not found.", ErrorCodes.NotFound);

        var absolute = ResolveAbsolute(attachment.StoragePath);
        if (!File.Exists(absolute))
            return ServiceResult<InvoicePdfFile>.NotFound("Attachment file missing.", ErrorCodes.NotFound);

        var bytes = await File.ReadAllBytesAsync(absolute, ct);
        return ServiceResult<InvoicePdfFile>.Ok(
            new InvoicePdfFile(bytes, attachment.FileName, attachment.ContentType));
    }

    public async Task<ServiceResult> DeleteAsync(Guid attachmentId, CancellationToken ct = default)
    {
        var companyId = await tenant.GetCompanyIdAsync(ct);
        var attachment = await db.InvoiceAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.CompanyId == companyId, ct);
        if (attachment is null)
            return ServiceResult.NotFound("Attachment not found.", ErrorCodes.NotFound);

        var absolute = ResolveAbsolute(attachment.StoragePath);
        db.InvoiceAttachments.Remove(attachment);
        await db.SaveChangesAsync(ct);

        try
        {
            if (File.Exists(absolute))
                File.Delete(absolute);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete attachment file {Path}", absolute);
        }

        return ServiceResult.Ok(StatusCodes.Status204NoContent);
    }

    private string GetInvoiceDirectory(Guid companyId, Guid invoiceId)
    {
        var root = options.Value.RootPath;
        var basePath = Path.IsPathRooted(root) ? root : Path.Combine(env.ContentRootPath, root);
        return Path.Combine(basePath, "attachments", companyId.ToString("N"), invoiceId.ToString("N"));
    }

    private string ResolveAbsolute(string relative)
    {
        var root = options.Value.RootPath;
        var basePath = Path.IsPathRooted(root) ? root : Path.Combine(env.ContentRootPath, root);
        var combined = Path.GetFullPath(Path.Combine(basePath, relative.Replace('/', Path.DirectorySeparatorChar)));
        var rootFull = Path.GetFullPath(basePath);
        if (!combined.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid attachment path.");
        return combined;
    }

    private static string? ExtensionForContentType(string contentType) => contentType switch
    {
        "image/png" => ".png",
        "image/jpeg" or "image/jpg" => ".jpg",
        "image/webp" => ".webp",
        "application/pdf" => ".pdf",
        "image/heic" => ".heic",
        "image/heif" => ".heif",
        _ => null,
    };
}
