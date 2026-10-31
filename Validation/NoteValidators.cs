using Fakturio.Api.Dtos;
using FluentValidation;

namespace Fakturio.Api.Validation;

public sealed class CreateNoteRequestValidator : AbstractValidator<CreateNoteRequest>
{
    public CreateNoteRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Názov je povinný.")
            .MaximumLength(200);
        RuleFor(x => x.Body).MaximumLength(16000);
    }
}

public sealed class UpdateNoteRequestValidator : AbstractValidator<UpdateNoteRequest>
{
    public UpdateNoteRequestValidator()
    {
        RuleFor(x => x.Title)
            .MaximumLength(200)
            .When(x => x.Title is not null);
        RuleFor(x => x.Body)
            .MaximumLength(16000)
            .When(x => x.Body is not null);
    }
}
