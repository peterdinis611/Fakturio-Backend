using Fakturio.Api.Common;
using Fakturio.Api.Dtos;
using FluentValidation;

namespace Fakturio.Api.Validation;

public sealed class SendTestEmailRequestValidator : AbstractValidator<SendTestEmailRequest>
{
    public SendTestEmailRequestValidator()
    {
        RuleFor(x => x.To)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.To))
            .WithMessage("Neplatný e-mail príjemcu.")
            .WithErrorCode(ErrorCodes.InvalidEmail);
    }
}
