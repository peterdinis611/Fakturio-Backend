using Fakturio.Api.Dtos;
using Fakturio.Api.Models;

namespace Fakturio.Api.Mapping;

public static class CompanyMapper
{
    public static CompanySettingsResponse ToResponse(CompanySettings c) => new(
        c.Id,
        c.Name,
        c.LegalForm,
        c.Ico,
        c.Dic,
        c.IcDph,
        c.IsVatPayer,
        c.CommercialRegister,
        c.Email,
        c.InvoiceEmail,
        c.Phone,
        c.Website,
        c.ContactPerson,
        c.Street,
        c.City,
        c.PostalCode,
        c.Country,
        c.BankIban,
        c.BankBic,
        c.BankName,
        c.DefaultDueDays,
        c.DefaultVatRate,
        c.DefaultCurrency,
        c.InvoicePrefix,
        c.DefaultPaymentMethod,
        c.DefaultConstantSymbol,
        c.DefaultInvoiceNotes,
        c.InvoiceFooter,
        c.LogoUrl,
        c.Notes,
        c.CreatedAt,
        c.UpdatedAt,
        BuildCompleteness(c)
    );

    public static CompanySummaryResponse ToSummary(CompanySettings c)
    {
        var completeness = BuildCompleteness(c);
        return new CompanySummaryResponse(
            c.Id,
            c.Name,
            c.LegalForm,
            c.Ico,
            c.Email,
            c.City,
            c.Country,
            c.IsVatPayer,
            c.LogoUrl,
            completeness.IsReadyToInvoice);
    }

    public static CompanyCompletenessResponse BuildCompleteness(CompanySettings c)
    {
        var items = new List<CompanyCompletenessItem>
        {
            Item("name", "Company name", true, !string.IsNullOrWhiteSpace(c.Name),
                "Set the legal company name shown on invoices."),
            Item("ico", "IČO", true, !string.IsNullOrWhiteSpace(c.Ico),
                "Slovak company identification number."),
            Item("address", "Address", true,
                !string.IsNullOrWhiteSpace(c.Street) && !string.IsNullOrWhiteSpace(c.City) && !string.IsNullOrWhiteSpace(c.PostalCode),
                "Street, city and postal code are required."),
            Item("email", "Email", true, !string.IsNullOrWhiteSpace(c.Email),
                "Contact email for the company profile."),
            Item("iban", "Bank IBAN", true, !string.IsNullOrWhiteSpace(c.BankIban),
                "IBAN is used as the default payment account on invoices."),
            Item("dic", "DIČ", false, !string.IsNullOrWhiteSpace(c.Dic),
                "Recommended for Slovak businesses."),
            Item("ic_dph", "IČ DPH", c.IsVatPayer, !string.IsNullOrWhiteSpace(c.IcDph),
                c.IsVatPayer ? "Required when the company is a VAT payer." : "Optional when not a VAT payer."),
            Item("payment_method", "Default payment method", false,
                !string.IsNullOrWhiteSpace(c.DefaultPaymentMethod),
                "Usually bank_transfer."),
            Item("footer", "Invoice footer", false, !string.IsNullOrWhiteSpace(c.InvoiceFooter),
                "Optional legal / thank-you text under the invoice.")
        };

        var required = items.Where(i => i.IsRequired).ToList();
        var completedRequired = required.Count(i => i.IsComplete);
        var percent = required.Count == 0
            ? 100
            : (int)Math.Round(completedRequired * 100.0 / required.Count);

        return new CompanyCompletenessResponse(
            required.All(i => i.IsComplete),
            completedRequired,
            required.Count,
            percent,
            items);
    }

    public static void Apply(CompanySettings company, UpdateCompanySettingsRequest request)
    {
        company.Name = request.Name.Trim();
        company.LegalForm = Normalize(request.LegalForm);
        company.Ico = Normalize(request.Ico);
        company.Dic = Normalize(request.Dic);
        company.IcDph = Normalize(request.IcDph)?.ToUpperInvariant();
        company.IsVatPayer = request.IsVatPayer ?? company.IsVatPayer;
        company.CommercialRegister = Normalize(request.CommercialRegister);
        company.Email = Normalize(request.Email);
        company.InvoiceEmail = Normalize(request.InvoiceEmail);
        company.Phone = Normalize(request.Phone);
        company.Website = Normalize(request.Website);
        company.ContactPerson = Normalize(request.ContactPerson);
        company.Street = Normalize(request.Street);
        company.City = Normalize(request.City);
        company.PostalCode = Normalize(request.PostalCode);
        company.Country = string.IsNullOrWhiteSpace(request.Country)
            ? "SK"
            : request.Country.Trim().ToUpperInvariant();
        company.BankIban = Normalize(request.BankIban)?.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
        company.BankBic = Normalize(request.BankBic)?.ToUpperInvariant();
        company.BankName = Normalize(request.BankName);
        company.DefaultDueDays = request.DefaultDueDays ?? company.DefaultDueDays;
        company.DefaultVatRate = request.DefaultVatRate ?? company.DefaultVatRate;
        company.DefaultCurrency = string.IsNullOrWhiteSpace(request.DefaultCurrency)
            ? company.DefaultCurrency
            : request.DefaultCurrency.Trim().ToUpperInvariant();
        company.InvoicePrefix = Normalize(request.InvoicePrefix);
        company.DefaultPaymentMethod = Normalize(request.DefaultPaymentMethod) ?? company.DefaultPaymentMethod;
        company.DefaultConstantSymbol = Normalize(request.DefaultConstantSymbol);
        company.DefaultInvoiceNotes = Normalize(request.DefaultInvoiceNotes);
        company.InvoiceFooter = Normalize(request.InvoiceFooter);
        company.LogoUrl = Normalize(request.LogoUrl);
        company.Notes = Normalize(request.Notes);
        company.UpdatedAt = DateTime.UtcNow;
    }

    public static void ApplyDefaults(CompanySettings company, UpdateCompanyDefaultsRequest request)
    {
        if (request.DefaultDueDays.HasValue)
            company.DefaultDueDays = request.DefaultDueDays.Value;
        if (request.DefaultVatRate.HasValue)
            company.DefaultVatRate = request.DefaultVatRate.Value;
        if (!string.IsNullOrWhiteSpace(request.DefaultCurrency))
            company.DefaultCurrency = request.DefaultCurrency.Trim().ToUpperInvariant();
        if (request.InvoicePrefix is not null)
            company.InvoicePrefix = Normalize(request.InvoicePrefix);
        if (request.DefaultPaymentMethod is not null)
            company.DefaultPaymentMethod = Normalize(request.DefaultPaymentMethod);
        if (request.DefaultConstantSymbol is not null)
            company.DefaultConstantSymbol = Normalize(request.DefaultConstantSymbol);
        if (request.DefaultInvoiceNotes is not null)
            company.DefaultInvoiceNotes = Normalize(request.DefaultInvoiceNotes);
        if (request.InvoiceFooter is not null)
            company.InvoiceFooter = Normalize(request.InvoiceFooter);

        company.UpdatedAt = DateTime.UtcNow;
    }

    private static CompanyCompletenessItem Item(
        string key,
        string label,
        bool required,
        bool complete,
        string? hint) => new(key, label, required, complete, hint);

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
