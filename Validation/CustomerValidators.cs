using Fakturio.Api.Dtos;
using FluentValidation;

namespace Fakturio.Api.Validation;

public class CreateCustomerRequestValidator : AbstractValidator<CreateCustomerRequest>
{
    public CreateCustomerRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
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
    }
}

public class UpdateCustomerRequestValidator : AbstractValidator<UpdateCustomerRequest>
{
    public UpdateCustomerRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
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
    }
}
