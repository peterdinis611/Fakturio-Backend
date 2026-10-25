using Fakturio.Api.Dtos;
using FluentValidation;

namespace Fakturio.Api.Validation;

public class UpdateCompanySettingsRequestValidator : AbstractValidator<UpdateCompanySettingsRequest>
{
    public UpdateCompanySettingsRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Company name is required.")
            .MaximumLength(200);

        RuleFor(x => x.Ico)
            .Must(SlovakValidators.BeValidIco)
            .WithMessage("IČO must be 8 digits with a valid checksum.");

        RuleFor(x => x.Dic)
            .Must(SlovakValidators.BeValidDic)
            .WithMessage("DIČ must be exactly 10 digits.");

        RuleFor(x => x.IcDph)
            .Must(SlovakValidators.BeValidIcDph)
            .WithMessage("IČ DPH must match format SK + 10 digits.");

        RuleFor(x => x.Email)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Email))
            .MaximumLength(200);

        RuleFor(x => x.Phone).MaximumLength(50);
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
            .When(x => x.DefaultDueDays.HasValue)
            .WithMessage("Default due days must be between 0 and 365.");

        RuleFor(x => x.DefaultVatRate)
            .Must(rate => rate is null || SlovakValidators.BeAllowedVatRate(rate.Value))
            .WithMessage("Default VAT rate must be one of: 0, 5, 19, 23.");

        RuleFor(x => x.DefaultCurrency)
            .Must(SlovakValidators.BeValidCurrency)
            .WithMessage("Currency must be a 3-letter ISO code.");

        RuleFor(x => x.InvoicePrefix)
            .MaximumLength(20)
            .Matches(@"^[A-Za-z0-9\-]*$")
            .When(x => !string.IsNullOrWhiteSpace(x.InvoicePrefix))
            .WithMessage("Invoice prefix may contain letters, digits and dashes only.");

        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}
