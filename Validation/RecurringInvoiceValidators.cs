using Fakturio.Api.Dtos;
using FluentValidation;

namespace Fakturio.Api.Validation;

public sealed class RecurringInvoiceItemRequestValidator : AbstractValidator<RecurringInvoiceItemRequest>
{
    public RecurringInvoiceItemRequestValidator()
    {
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Popis je povinný.")
            .MaximumLength(500);

        RuleFor(x => x.Unit)
            .MaximumLength(20)
            .When(x => x.Unit is not null);

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Množstvo musí byť > 0.");

        RuleFor(x => x.UnitPrice)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.VatRate)
            .InclusiveBetween(0, 100)
            .When(x => x.VatRate.HasValue);
    }
}

public sealed class CreateRecurringInvoiceRequestValidator : AbstractValidator<CreateRecurringInvoiceRequest>
{
    public CreateRecurringInvoiceRequestValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Interval).IsInEnum();
        RuleFor(x => x.DayOfMonth)
            .InclusiveBetween(1, 28)
            .When(x => x.DayOfMonth.HasValue);
        RuleFor(x => x.DueDays)
            .InclusiveBetween(0, 365)
            .When(x => x.DueDays.HasValue);
        RuleFor(x => x.Notes).MaximumLength(2000).When(x => x.Notes is not null);
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Pridaj aspoň jednu položku.")
            .Must(i => i.Count <= 50);
        RuleForEach(x => x.Items).SetValidator(new RecurringInvoiceItemRequestValidator());
    }
}

public sealed class UpdateRecurringInvoiceRequestValidator : AbstractValidator<UpdateRecurringInvoiceRequest>
{
    public UpdateRecurringInvoiceRequestValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Interval).IsInEnum();
        RuleFor(x => x.DayOfMonth)
            .InclusiveBetween(1, 28)
            .When(x => x.DayOfMonth.HasValue);
        RuleFor(x => x.DueDays)
            .InclusiveBetween(0, 365)
            .When(x => x.DueDays.HasValue);
        RuleFor(x => x.Notes).MaximumLength(2000).When(x => x.Notes is not null);
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Pridaj aspoň jednu položku.")
            .Must(i => i.Count <= 50);
        RuleForEach(x => x.Items).SetValidator(new RecurringInvoiceItemRequestValidator());
    }
}
