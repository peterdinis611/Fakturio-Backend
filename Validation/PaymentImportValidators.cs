using Fakturio.Api.Dtos;
using FluentValidation;

namespace Fakturio.Api.Validation;

public sealed class PaymentImportPreviewRequestValidator
    : AbstractValidator<PaymentImportPreviewRequest>
{
    public PaymentImportPreviewRequestValidator()
    {
        RuleFor(x => x.CsvText)
            .NotEmpty().WithMessage("CSV text is required.")
            .MaximumLength(2_000_000);
    }
}

public sealed class PaymentImportConfirmRequestValidator
    : AbstractValidator<PaymentImportConfirmRequest>
{
    public PaymentImportConfirmRequestValidator()
    {
        RuleFor(x => x.Rows)
            .NotEmpty().WithMessage("At least one row is required.")
            .Must(rows => rows.Count <= 500)
            .WithMessage("At most 500 rows can be confirmed at once.");

        RuleForEach(x => x.Rows).ChildRules(row =>
        {
            row.RuleFor(r => r.InvoiceId).NotEmpty();
        });
    }
}
