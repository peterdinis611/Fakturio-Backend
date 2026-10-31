using Fakturio.Api.Dtos;
using FluentValidation;

namespace Fakturio.Api.Validation;

public class UpdateCompanySettingsRequestValidator : AbstractValidator<UpdateCompanySettingsRequest>
{
    private static readonly HashSet<string> AllowedPaymentMethods =
        ["bank_transfer", "cash", "card", "other"];

    private static readonly HashSet<string> AllowedLegalForms =
    [
        "s.r.o.", "a.s.", "k.s.", "v.o.s.", "živnosť", "SZČO", "družstvo", "iné"
    ];

    public UpdateCompanySettingsRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Company name is required.")
            .MaximumLength(200);

        RuleFor(x => x.LegalForm)
            .Must(f => f is null || AllowedLegalForms.Contains(f))
            .WithMessage("Legal form must be one of: s.r.o., a.s., k.s., v.o.s., živnosť, SZČO, družstvo, iné.");

        RuleFor(x => x.Ico)
            .Must(SlovakValidators.BeValidIco)
            .WithMessage("IČO must be 8 digits with a valid checksum.");

        RuleFor(x => x.Dic)
            .Must(SlovakValidators.BeValidDic)
            .WithMessage("DIČ must be exactly 10 digits.");

        RuleFor(x => x.IcDph)
            .Must(SlovakValidators.BeValidIcDph)
            .WithMessage("IČ DPH must match format SK + 10 digits.");

        RuleFor(x => x.IcDph)
            .NotEmpty()
            .When(x => x.IsVatPayer == true)
            .WithMessage("IČ DPH is required when the company is a VAT payer.");

        RuleFor(x => x.CommercialRegister).MaximumLength(500);

        RuleFor(x => x.Email)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Email))
            .MaximumLength(200);

        RuleFor(x => x.InvoiceEmail)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.InvoiceEmail))
            .MaximumLength(200);

        RuleFor(x => x.Phone).MaximumLength(50);
        RuleFor(x => x.Website)
            .MaximumLength(300)
            .Must(BeValidAbsoluteUrl)
            .When(x => !string.IsNullOrWhiteSpace(x.Website))
            .WithMessage("Website must be a valid http(s) URL.");

        RuleFor(x => x.ContactPerson).MaximumLength(200);
        RuleFor(x => x.Street).MaximumLength(200);
        RuleFor(x => x.City).MaximumLength(100);

        RuleFor(x => x.PostalCode)
            .Must(SlovakValidators.BeValidPostalCode)
            .WithMessage("Postal code must look like 811 01.");

        RuleFor(x => x.Country)
            .Must(SlovakValidators.BeValidCountryCode)
            .WithMessage("Country must be a 2-letter ISO code.");

        RuleFor(x => x.BankIban)
            .Must(SlovakValidators.BeValidIban)
            .WithMessage("IBAN is invalid.");

        RuleFor(x => x.BankBic)
            .Must(SlovakValidators.BeValidBic)
            .WithMessage("BIC/SWIFT is invalid.");

        RuleFor(x => x.BankName).MaximumLength(100);

        RuleFor(x => x.DefaultDueDays)
            .InclusiveBetween(0, 365)
            .When(x => x.DefaultDueDays.HasValue);

        RuleFor(x => x.DefaultVatRate)
            .Must(rate => rate is null || SlovakValidators.BeAllowedVatRate(rate.Value))
            .WithMessage("Default VAT rate must be one of: 0, 5, 19, 23.");

        RuleFor(x => x.DefaultCurrency)
            .Must(SlovakValidators.BeValidCurrency);

        RuleFor(x => x.InvoicePrefix)
            .MaximumLength(20)
            .Matches(@"^[A-Za-z0-9\-]*$")
            .When(x => !string.IsNullOrWhiteSpace(x.InvoicePrefix));

        RuleFor(x => x.DefaultPaymentMethod)
            .Must(m => m is null || AllowedPaymentMethods.Contains(m))
            .WithMessage("Payment method must be bank_transfer, cash, card, or other.");

        RuleFor(x => x.DefaultConstantSymbol)
            .Matches(@"^\d{1,4}$")
            .When(x => !string.IsNullOrWhiteSpace(x.DefaultConstantSymbol));

        RuleFor(x => x.DefaultInvoiceNotes).MaximumLength(2000);
        RuleFor(x => x.InvoiceFooter).MaximumLength(2000);
        RuleFor(x => x.Notes).MaximumLength(2000);

        RuleFor(x => x.LogoUrl)
            .MaximumLength(500)
            .Must(BeValidLogoUrl)
            .When(x => !string.IsNullOrWhiteSpace(x.LogoUrl))
            .WithMessage("LogoUrl must be a valid http(s) URL or /uploads path.");
    }

    private static bool BeValidAbsoluteUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static bool BeValidLogoUrl(string? url) =>
        (!string.IsNullOrWhiteSpace(url) && url.StartsWith("/uploads/", StringComparison.Ordinal))
        || BeValidAbsoluteUrl(url);
}

public class UpdateCompanyDefaultsRequestValidator : AbstractValidator<UpdateCompanyDefaultsRequest>
{
    private static readonly HashSet<string> AllowedPaymentMethods =
        ["bank_transfer", "cash", "card", "other"];

    public UpdateCompanyDefaultsRequestValidator()
    {
        RuleFor(x => x.DefaultDueDays)
            .InclusiveBetween(0, 365)
            .When(x => x.DefaultDueDays.HasValue);

        RuleFor(x => x.DefaultVatRate)
            .Must(rate => rate is null || SlovakValidators.BeAllowedVatRate(rate.Value));

        RuleFor(x => x.DefaultCurrency)
            .Must(SlovakValidators.BeValidCurrency);

        RuleFor(x => x.InvoicePrefix)
            .MaximumLength(20)
            .Matches(@"^[A-Za-z0-9\-]*$")
            .When(x => !string.IsNullOrWhiteSpace(x.InvoicePrefix));

        RuleFor(x => x.DefaultPaymentMethod)
            .Must(m => m is null || AllowedPaymentMethods.Contains(m));

        RuleFor(x => x.DefaultConstantSymbol)
            .Matches(@"^\d{1,4}$")
            .When(x => !string.IsNullOrWhiteSpace(x.DefaultConstantSymbol));

        RuleFor(x => x.DefaultInvoiceNotes).MaximumLength(2000);
        RuleFor(x => x.InvoiceFooter).MaximumLength(2000);

        RuleFor(x => x)
            .Must(HasAtLeastOneField)
            .WithMessage("At least one default field must be provided.");
    }

    private static bool HasAtLeastOneField(UpdateCompanyDefaultsRequest x) =>
        x.DefaultDueDays.HasValue
        || x.DefaultVatRate.HasValue
        || x.DefaultCurrency is not null
        || x.InvoicePrefix is not null
        || x.DefaultPaymentMethod is not null
        || x.DefaultConstantSymbol is not null
        || x.DefaultInvoiceNotes is not null
        || x.InvoiceFooter is not null;
}

public class UpdateCompanyBankRequestValidator : AbstractValidator<UpdateCompanyBankRequest>
{
    private static readonly HashSet<string> AllowedPaymentMethods =
        ["bank_transfer", "cash", "card", "other"];

    public UpdateCompanyBankRequestValidator()
    {
        RuleFor(x => x.BankIban)
            .Must(SlovakValidators.BeValidIban)
            .WithMessage("IBAN is invalid.");

        RuleFor(x => x.BankBic)
            .Must(SlovakValidators.BeValidBic)
            .WithMessage("BIC/SWIFT is invalid.");

        RuleFor(x => x.BankName).MaximumLength(100);

        RuleFor(x => x.DefaultPaymentMethod)
            .Must(m => m is null || AllowedPaymentMethods.Contains(m))
            .WithMessage("Payment method must be bank_transfer, cash, card, or other.");

        RuleFor(x => x.DefaultConstantSymbol)
            .Matches(@"^\d{1,4}$")
            .When(x => !string.IsNullOrWhiteSpace(x.DefaultConstantSymbol));

        RuleFor(x => x)
            .Must(HasAtLeastOneField)
            .WithMessage("At least one bank field must be provided.");
    }

    private static bool HasAtLeastOneField(UpdateCompanyBankRequest x) =>
        x.BankIban is not null
        || x.BankBic is not null
        || x.BankName is not null
        || x.DefaultPaymentMethod is not null
        || x.DefaultConstantSymbol is not null;
}

public class UpdateCompanyBrandingRequestValidator : AbstractValidator<UpdateCompanyBrandingRequest>
{
    public UpdateCompanyBrandingRequestValidator()
    {
        RuleFor(x => x.LogoUrl)
            .MaximumLength(500)
            .Must(BeValidLogoUrl)
            .When(x => !string.IsNullOrWhiteSpace(x.LogoUrl))
            .WithMessage("LogoUrl must be a valid http(s) URL or /uploads path.");

        RuleFor(x => x.Website)
            .MaximumLength(300)
            .Must(BeValidAbsoluteUrl)
            .When(x => !string.IsNullOrWhiteSpace(x.Website))
            .WithMessage("Website must be a valid http(s) URL.");

        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.InvoiceFooter).MaximumLength(2000);

        RuleFor(x => x)
            .Must(HasAtLeastOneField)
            .WithMessage("At least one branding field must be provided.");
    }

    private static bool HasAtLeastOneField(UpdateCompanyBrandingRequest x) =>
        x.LogoUrl is not null
        || x.Website is not null
        || x.Notes is not null
        || x.InvoiceFooter is not null;

    private static bool BeValidAbsoluteUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static bool BeValidLogoUrl(string? url) =>
        (!string.IsNullOrWhiteSpace(url) && url.StartsWith("/uploads/", StringComparison.Ordinal))
        || BeValidAbsoluteUrl(url);
}

public class UpdateCompanyContactRequestValidator : AbstractValidator<UpdateCompanyContactRequest>
{
    public UpdateCompanyContactRequestValidator()
    {
        RuleFor(x => x.Email)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Email))
            .MaximumLength(200);

        RuleFor(x => x.InvoiceEmail)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.InvoiceEmail))
            .MaximumLength(200);

        RuleFor(x => x.Phone).MaximumLength(50);
        RuleFor(x => x.ContactPerson).MaximumLength(200);
        RuleFor(x => x.Street).MaximumLength(200);
        RuleFor(x => x.City).MaximumLength(100);

        RuleFor(x => x.PostalCode)
            .Must(SlovakValidators.BeValidPostalCode)
            .WithMessage("Postal code must look like 811 01.");

        RuleFor(x => x.Country)
            .Must(SlovakValidators.BeValidCountryCode)
            .WithMessage("Country must be a 2-letter ISO code.");

        RuleFor(x => x)
            .Must(HasAtLeastOneField)
            .WithMessage("At least one contact field must be provided.");
    }

    private static bool HasAtLeastOneField(UpdateCompanyContactRequest x) =>
        x.Email is not null
        || x.InvoiceEmail is not null
        || x.Phone is not null
        || x.ContactPerson is not null
        || x.Street is not null
        || x.City is not null
        || x.PostalCode is not null
        || x.Country is not null;
}
