using System.Net.Mail;
using Fakturio.Api.Common;
using Fakturio.Api.Configuration;
using Fakturio.Api.Data;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Models;
using Fakturio.Api.Services.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Fakturio.Api.Services.Email;

public sealed class InvoiceEmailService(
    AppDbContext db,
    ITenantContext tenant,
    ICurrentActorAccessor actors,
    IEmailSender emailSender,
    IInvoicePdfService pdfs,
    IAuditLogService audit,
    IOptions<EmailOptions> options,
    ILogger<InvoiceEmailService> logger) : IInvoiceEmailService
{
    private readonly EmailOptions _options = options.Value;

    public EmailStatusResponse GetStatus() =>
        new(
            emailSender.IsConfigured,
            emailSender.ProviderName,
            string.IsNullOrWhiteSpace(_options.FromAddress) ? null : _options.FromAddress,
            string.IsNullOrWhiteSpace(_options.FromName) ? null : _options.FromName,
            !string.IsNullOrWhiteSpace(_options.PublicAppBaseUrl));

    public async Task<ServiceResult<SendTestEmailResponse>> SendTestAsync(
        SendTestEmailRequest request,
        CancellationToken ct = default)
    {
        if (!emailSender.IsConfigured)
            return NotConfigured<SendTestEmailResponse>();

        var actor = await actors.GetAsync(ct);
        var to = FirstNonEmpty(request.To, actor.Email);
        if (string.IsNullOrWhiteSpace(to) || !IsValidEmail(to))
            return ServiceResult<SendTestEmailResponse>.BadRequest(
                "Zadaj platný e-mail príjemcu.",
                ErrorCodes.InvalidEmail);

        var (html, text) = EmailTemplates.Test(_options.FromName);
        try
        {
            await emailSender.SendAsync(
                new OutboundEmail(
                    to.Trim(),
                    "Fakturio · skúšobný e-mail",
                    html,
                    text),
                ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Test email failed to {To}", to);
            return ServiceResult<SendTestEmailResponse>.Fail(
                "Odoslanie skúšobného e-mailu zlyhalo.",
                StatusCodes.Status502BadGateway,
                ErrorCodes.EmailSendFailed);
        }

        return ServiceResult<SendTestEmailResponse>.Ok(
            new SendTestEmailResponse(to.Trim(), emailSender.ProviderName));
    }

    public async Task<ServiceResult<SendInvoiceEmailResponse>> SendInvoiceAsync(
        Guid invoiceId,
        SendInvoiceEmailRequest request,
        CancellationToken ct = default)
    {
        if (!emailSender.IsConfigured)
            return NotConfigured<SendInvoiceEmailResponse>();

        var companyId = await tenant.GetCompanyIdAsync(ct);
        var invoice = await db.Invoices
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.CompanyId == companyId, ct);

        if (invoice is null)
            return ServiceResult<SendInvoiceEmailResponse>.NotFound(
                "Invoice not found.",
                ErrorCodes.InvoiceNotFound);

        if (invoice.Status is InvoiceStatus.Draft or InvoiceStatus.Cancelled)
            return ServiceResult<SendInvoiceEmailResponse>.BadRequest(
                "E-mail je možné poslať len pre vystavené doklady.",
                ErrorCodes.InvalidStatus);

        var to = FirstNonEmpty(request.To, invoice.CustomerEmail);
        if (string.IsNullOrWhiteSpace(to) || !IsValidEmail(to))
            return ServiceResult<SendInvoiceEmailResponse>.BadRequest(
                "Zákazník nemá platný e-mail. Zadaj príjemcu.",
                ErrorCodes.InvalidEmail);

        var company = await db.CompanySettings.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == companyId, ct);

        var companyName = company?.Name ?? _options.FromName;
        // Keep system FromAddress (domain must be verified on Resend/SMTP);
        // company contact goes to Reply-To.
        var replyTo = FirstNonEmpty(company?.InvoiceEmail, company?.Email, _options.ReplyTo);

        string? publicUrl = null;
        var includePublic = request.IncludePublicLink
            && invoice.PublicLinkEnabled
            && !string.IsNullOrWhiteSpace(invoice.PublicAccessToken);
        if (includePublic)
            publicUrl = BuildPublicUrl(invoice.PublicAccessToken!);

        var (html, text) = EmailTemplates.Invoice(
            companyName,
            invoice,
            request.Message,
            publicUrl);

        var subject = string.IsNullOrWhiteSpace(request.Subject)
            ? $"Doklad {invoice.Number} · {invoice.Total:0.00} {invoice.Currency}"
            : request.Subject.Trim();

        List<EmailAttachment>? attachments = null;
        var pdfAttached = false;
        if (request.AttachPdf)
        {
            var pdf = await pdfs.GenerateAsync(invoiceId, ct);
            if (!pdf.Success || pdf.Data is null)
            {
                return ServiceResult<SendInvoiceEmailResponse>.Fail(
                    pdf.Error ?? "PDF sa nepodarilo vygenerovať.",
                    pdf.StatusCode == 0 ? StatusCodes.Status500InternalServerError : pdf.StatusCode,
                    pdf.ErrorCode ?? ErrorCodes.EmailSendFailed);
            }

            attachments =
            [
                new EmailAttachment(
                    pdf.Data.FileName,
                    pdf.Data.ContentType,
                    pdf.Data.Content)
            ];
            pdfAttached = true;
        }

        try
        {
            await emailSender.SendAsync(
                new OutboundEmail(
                    to.Trim(),
                    subject,
                    html,
                    text,
                    replyTo,
                    FromNameOverride: companyName,
                    Attachments: attachments),
                ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Invoice email failed for {Number} → {To}", invoice.Number, to);
            return ServiceResult<SendInvoiceEmailResponse>.Fail(
                "Odoslanie e-mailu zlyhalo.",
                StatusCodes.Status502BadGateway,
                ErrorCodes.EmailSendFailed);
        }

        await audit.WriteAsync(
            AuditAction.EmailSent,
            "Invoice",
            invoice.Id,
            invoice.Number,
            $"Doklad {invoice.Number} odoslaný e-mailom na {to.Trim()}.",
            new
            {
                to = to.Trim(),
                provider = emailSender.ProviderName,
                pdfAttached,
                publicLink = publicUrl is not null,
            },
            ct);

        return ServiceResult<SendInvoiceEmailResponse>.Ok(
            new SendInvoiceEmailResponse(
                invoice.Id,
                invoice.Number,
                to.Trim(),
                emailSender.ProviderName,
                pdfAttached,
                publicUrl is not null));
    }

    public async Task<bool> TrySendReminderAsync(
        InvoiceReminderItemResponse reminder,
        Guid companyId,
        Guid userId,
        CancellationToken ct = default)
    {
        if (!emailSender.IsConfigured)
            return false;

        if (string.IsNullOrWhiteSpace(reminder.CustomerEmail) || !IsValidEmail(reminder.CustomerEmail))
        {
            logger.LogDebug(
                "Reminder email skipped for {Number}: no customer email.",
                reminder.Number);
            return false;
        }

        var company = await db.CompanySettings.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == companyId, ct);
        if (company is null)
            return false;

        var companyName = company.Name;
        var replyTo = FirstNonEmpty(company.InvoiceEmail, company.Email, _options.ReplyTo);
        var (html, text, subject) = EmailTemplates.Reminder(companyName, reminder);

        try
        {
            await emailSender.SendAsync(
                new OutboundEmail(
                    reminder.CustomerEmail.Trim(),
                    subject,
                    html,
                    text,
                    replyTo,
                    FromNameOverride: companyName),
                ct);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Reminder email failed for {Number} → {Email}",
                reminder.Number,
                reminder.CustomerEmail);
            return false;
        }

        await audit.WriteAsync(
            AuditAction.EmailSent,
            "Invoice",
            reminder.Id,
            reminder.Number,
            $"Upomienka {reminder.Number} odoslaná e-mailom na {reminder.CustomerEmail}.",
            companyId,
            new
            {
                to = reminder.CustomerEmail,
                provider = emailSender.ProviderName,
                kind = "reminder",
                userId,
            },
            ct);

        return true;
    }

    private string? BuildPublicUrl(string token)
    {
        var path = $"/p/{token}";
        var baseUrl = _options.PublicAppBaseUrl?.Trim().TrimEnd('/');
        return string.IsNullOrWhiteSpace(baseUrl) ? path : $"{baseUrl}{path}";
    }

    private ServiceResult<T> NotConfigured<T>() =>
        ServiceResult<T>.Fail(
            "E-mailový provider nie je nakonfigurovaný. Nastav Email:Provider (Virtual, Smtp alebo Resend).",
            StatusCodes.Status503ServiceUnavailable,
            ErrorCodes.EmailNotConfigured);

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private static bool IsValidEmail(string email)
    {
        try
        {
            _ = new MailAddress(email.Trim());
            return email.Contains('@', StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }
}
