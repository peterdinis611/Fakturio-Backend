using Fakturio.Api.Dtos;
using Fakturio.Api.Models;
using FluentValidation;

namespace Fakturio.Api.Validation;

public sealed class UpdateResumeProfileRequestValidator
    : AbstractValidator<UpdateResumeProfileRequest>
{
    public UpdateResumeProfileRequestValidator()
    {
        RuleFor(x => x.FullName).MaximumLength(200);
        RuleFor(x => x.Headline).MaximumLength(300);
        RuleFor(x => x.Summary).MaximumLength(4000);
        RuleFor(x => x.Email)
            .MaximumLength(200)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(50);
        RuleFor(x => x.Location).MaximumLength(200);
        RuleFor(x => x.Website).MaximumLength(300);
        RuleFor(x => x.Skills).MaximumLength(2000);
    }
}

public sealed class CreateResumeEntryRequestValidator
    : AbstractValidator<CreateResumeEntryRequest>
{
    public CreateResumeEntryRequestValidator()
    {
        RuleFor(x => x.Type)
            .NotEmpty()
            .Must(ResumeEntryTypes.IsValid)
            .WithMessage("Type must be experience, education, certification, language, or other.");
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200);
        RuleFor(x => x.Organization).MaximumLength(200);
        RuleFor(x => x.Location).MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x)
            .Must(x => x.IsCurrent == true || x.EndDate is null || x.StartDate is null || x.EndDate >= x.StartDate)
            .WithMessage("End date must be on or after start date.");
    }
}

public sealed class UpdateResumeEntryRequestValidator
    : AbstractValidator<UpdateResumeEntryRequest>
{
    public UpdateResumeEntryRequestValidator()
    {
        RuleFor(x => x.Type)
            .Must(t => t is null || ResumeEntryTypes.IsValid(t))
            .WithMessage("Type must be experience, education, certification, language, or other.");
        RuleFor(x => x.Title)
            .MaximumLength(200)
            .When(x => x.Title is not null);
        RuleFor(x => x.Organization).MaximumLength(200);
        RuleFor(x => x.Location).MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x)
            .Must(x => x.IsCurrent == true || x.EndDate is null || x.StartDate is null || x.EndDate >= x.StartDate)
            .WithMessage("End date must be on or after start date.");
    }
}
