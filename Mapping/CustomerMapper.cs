using Fakturio.Api.Dtos;
using Fakturio.Api.Models;

namespace Fakturio.Api.Mapping;

public static class CustomerMapper
{
    public static CustomerResponse ToResponse(Customer c, int invoiceCount = 0) => new(
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
        c.CreatedAt,
        invoiceCount
    );

    public static CustomerLookupItem ToLookupItem(Customer c) => new(
        c.Id,
        c.Name,
        c.Ico,
        c.City);

    public static void Apply(Customer customer, CreateCustomerRequest request)
    {
        customer.Name = request.Name.Trim();
        customer.Ico = Validation.SlovakValidators.NormalizeIco(request.Ico);
        customer.Dic = NormalizeOptional(request.Dic);
        customer.IcDph = NormalizeOptional(request.IcDph)?.ToUpperInvariant();
        customer.Email = NormalizeOptional(request.Email);
        customer.Phone = NormalizeOptional(request.Phone);
        customer.Street = NormalizeOptional(request.Street);
        customer.City = NormalizeOptional(request.City);
        customer.PostalCode = NormalizeOptional(request.PostalCode);
        customer.Country = string.IsNullOrWhiteSpace(request.Country)
            ? "SK"
            : request.Country.Trim().ToUpperInvariant();
    }

    public static void Apply(Customer customer, UpdateCustomerRequest request)
    {
        customer.Name = request.Name.Trim();
        customer.Ico = Validation.SlovakValidators.NormalizeIco(request.Ico);
        customer.Dic = NormalizeOptional(request.Dic);
        customer.IcDph = NormalizeOptional(request.IcDph)?.ToUpperInvariant();
        customer.Email = NormalizeOptional(request.Email);
        customer.Phone = NormalizeOptional(request.Phone);
        customer.Street = NormalizeOptional(request.Street);
        customer.City = NormalizeOptional(request.City);
        customer.PostalCode = NormalizeOptional(request.PostalCode);
        customer.Country = string.IsNullOrWhiteSpace(request.Country)
            ? customer.Country
            : request.Country.Trim().ToUpperInvariant();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
