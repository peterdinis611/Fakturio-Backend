using Fakturio.Api.Dtos;
using FluentValidation;

namespace Fakturio.Api.Validation;

public sealed class CreateServiceCatalogItemRequestValidator
    : AbstractValidator<CreateServiceCatalogItemRequest>
{
    public CreateServiceCatalogItemRequestValidator()
    {
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(500);

        RuleFor(x => x.Unit).MaximumLength(20);
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.VatRate)
            .Must(rate => rate is null || SlovakValidators.BeAllowedVatRate(rate.Value))
            .WithMessage("VAT rate must be an allowed Slovak rate.");
    }
}

public sealed class UpdateServiceCatalogItemRequestValidator
    : AbstractValidator<UpdateServiceCatalogItemRequest>
{
    public UpdateServiceCatalogItemRequestValidator()
    {
        RuleFor(x => x.Description)
            .MaximumLength(500)
            .When(x => x.Description is not null);

        RuleFor(x => x.Unit).MaximumLength(20);
        RuleFor(x => x.UnitPrice)
            .GreaterThanOrEqualTo(0)
            .When(x => x.UnitPrice.HasValue);
        RuleFor(x => x.VatRate)
            .Must(rate => rate is null || SlovakValidators.BeAllowedVatRate(rate.Value))
            .WithMessage("VAT rate must be an allowed Slovak rate.");
    }
}
