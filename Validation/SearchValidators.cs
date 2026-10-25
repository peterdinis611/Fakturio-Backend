using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;
using FluentValidation;

namespace Fakturio.Api.Validation;

public class SearchRequestValidator : AbstractValidator<SearchRequest>
{
    public SearchRequestValidator()
    {
        RuleFor(x => x.Query)
            .NotEmpty().WithMessage("Search query is required.")
            .MinimumLength(ServiceGuards.DefaultMinSearchLength)
            .MaximumLength(ServiceGuards.DefaultMaxSearchLength);

        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 50);

        RuleFor(x => x.Types)
            .Must(t => t is null || t != SearchEntityType.None)
            .WithMessage("At least one search type is required.");

        RuleFor(x => x.InvoiceStatus)
            .IsInEnum()
            .When(x => x.InvoiceStatus.HasValue);
    }
}
