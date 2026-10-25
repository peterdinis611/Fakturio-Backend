using Fakturio.Api.Dtos;
using Fakturio.Api.Models;

namespace Fakturio.Api.Mapping;

public static class CompanyMapper
{
    public static CompanySettingsResponse ToResponse(CompanySettings c) => new(
        c.Id,
        c.Name,
        c.Ico,
        c.Dic,
        c.IcDph,
        c.Email,
        c.Phone,
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
        c.Notes,
        c.UpdatedAt
    );

    public static void Apply(CompanySettings company, UpdateCompanySettingsRequest request)
    {
        company.Name = request.Name.Trim();
        company.Ico = Normalize(request.Ico);
        company.Dic = Normalize(request.Dic);
        company.IcDph = Normalize(request.IcDph)?.ToUpperInvariant();
        company.Email = Normalize(request.Email);
        company.Phone = Normalize(request.Phone);
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
        company.Notes = Normalize(request.Notes);
        company.UpdatedAt = DateTime.UtcNow;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
