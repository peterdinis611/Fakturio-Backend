using Fakturio.Api.Dtos;
using FluentValidation;

namespace Fakturio.Api.Validation;

public sealed class DeleteAccountRequestValidator : AbstractValidator<DeleteAccountRequest>
{
    public DeleteAccountRequestValidator()
    {
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Heslo je povinné.");

        RuleFor(x => x.Confirmation)
            .NotEmpty().WithMessage("Potvrdenie je povinné.")
            .Must(v => string.Equals(v?.Trim(), "ZMAZAT", StringComparison.OrdinalIgnoreCase)
                || string.Equals(v?.Trim(), "DELETE", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Napíš ZMAZAT alebo DELETE na potvrdenie.");
    }
}
