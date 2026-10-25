using Fakturio.Api.Dtos;
using Fakturio.Api.Models;
using FluentValidation;

namespace Fakturio.Api.Validation;

public class CreateInvoiceItemRequestValidator : AbstractValidator<CreateInvoiceItemRequest>
{
    public CreateInvoiceItemRequestValidator()
    {
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Item description is required.")
            .MaximumLength(500);

        RuleFor(x => x.Unit).MaximumLength(20);

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Quantity must be greater than zero.")
            .LessThanOrEqualTo(1_000_000);

        RuleFor(x => x.UnitPrice)
            .GreaterThanOrEqualTo(0).WithMessage("Unit price cannot be negative.")
            .LessThanOrEqualTo(10_000_000);

        RuleFor(x => x.VatRate)
            .Must(rate => rate is null || SlovakValidators.BeAllowedVatRate(rate.Value))
            .WithMessage("VAT rate must be one of: 0, 5, 19, 23.");
    }
}

public class CreateInvoiceRequestValidator : AbstractValidator<CreateInvoiceRequest>
{
    public CreateInvoiceRequestValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("CustomerId is required.");

        RuleFor(x => x.Items)
            .NotNull().WithMessage("Items are required.")
            .Must(items => items is { Count: > 0 })
            .WithMessage("At least one invoice item is required.")
            .Must(items => items is null || items.Count <= 200)
            .WithMessage("Maximum 200 items per invoice.");

        RuleForEach(x => x.Items)
            .SetValidator(new CreateInvoiceItemRequestValidator());

        RuleFor(x => x.DueDate)
            .GreaterThanOrEqualTo(x => x.IssueDate ?? DateOnly.FromDateTime(DateTime.UtcNow))
            .When(x => x.DueDate.HasValue)
            .WithMessage("Due date cannot be before issue date.");

        RuleFor(x => x.DeliveryDate)
            .LessThanOrEqualTo(x => x.IssueDate ?? DateOnly.FromDateTime(DateTime.UtcNow))
            .When(x => x.DeliveryDate.HasValue)
            .WithMessage("Delivery date cannot be after issue date.");

        RuleFor(x => x.Currency)
            .Must(SlovakValidators.BeValidCurrency)
            .WithMessage("Currency must be a 3-letter ISO code.");

        RuleFor(x => x.VariableSymbol)
            .Must(SlovakValidators.BeValidVariableSymbol)
            .WithMessage("Variable symbol must be 1–10 digits.");

        RuleFor(x => x.ConstantSymbol)
            .Matches(@"^\d{1,4}$")
            .When(x => !string.IsNullOrWhiteSpace(x.ConstantSymbol))
            .WithMessage("Constant symbol must be 1–4 digits.");

        RuleFor(x => x.SpecificSymbol)
            .Must(SlovakValidators.BeValidVariableSymbol)
            .WithMessage("Specific symbol must be 1–10 digits.");

        RuleFor(x => x.PaymentMethod)
            .Must(m => m is null || AllowedPaymentMethods.Contains(m))
            .WithMessage("Payment method must be bank_transfer, cash, card, or other.");

        RuleFor(x => x.BankIban)
            .Must(SlovakValidators.BeValidIban)
            .WithMessage("IBAN is invalid.");

        RuleFor(x => x.BankBic)
            .Must(SlovakValidators.BeValidBic)
            .WithMessage("BIC/SWIFT is invalid.");

        RuleFor(x => x.Notes).MaximumLength(2000);

        RuleFor(x => x.Status)
            .Must(s => s is null or InvoiceStatus.Draft or InvoiceStatus.Issued)
            .WithMessage("New invoices can only be Draft or Issued.");
    }

    private static readonly HashSet<string> AllowedPaymentMethods =
        ["bank_transfer", "cash", "card", "other"];
}

public class UpdateInvoiceRequestValidator : AbstractValidator<UpdateInvoiceRequest>
{
    public UpdateInvoiceRequestValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();

        RuleFor(x => x.Items)
            .NotNull()
            .Must(items => items is { Count: > 0 })
            .WithMessage("At least one invoice item is required.")
            .Must(items => items is null || items.Count <= 200)
            .WithMessage("Maximum 200 items per invoice.");

        RuleForEach(x => x.Items)
            .SetValidator(new CreateInvoiceItemRequestValidator());

        RuleFor(x => x.DueDate)
            .GreaterThanOrEqualTo(x => x.IssueDate)
            .WithMessage("Due date cannot be before issue date.");

        RuleFor(x => x.DeliveryDate)
            .LessThanOrEqualTo(x => x.IssueDate)
            .When(x => x.DeliveryDate.HasValue)
            .WithMessage("Delivery date cannot be after issue date.");

        RuleFor(x => x.Currency)
            .Must(SlovakValidators.BeValidCurrency)
            .WithMessage("Currency must be a 3-letter ISO code.");

        RuleFor(x => x.VariableSymbol)
            .Must(SlovakValidators.BeValidVariableSymbol)
            .WithMessage("Variable symbol must be 1–10 digits.");

        RuleFor(x => x.ConstantSymbol)
            .Matches(@"^\d{1,4}$")
            .When(x => !string.IsNullOrWhiteSpace(x.ConstantSymbol));

        RuleFor(x => x.SpecificSymbol)
            .Must(SlovakValidators.BeValidVariableSymbol);

        RuleFor(x => x.PaymentMethod)
            .Must(m => m is null || AllowedPaymentMethods.Contains(m))
            .WithMessage("Payment method must be bank_transfer, cash, card, or other.");

        RuleFor(x => x.BankIban).Must(SlovakValidators.BeValidIban);
        RuleFor(x => x.BankBic).Must(SlovakValidators.BeValidBic);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }

    private static readonly HashSet<string> AllowedPaymentMethods =
        ["bank_transfer", "cash", "card", "other"];
}

public class UpdateInvoiceStatusRequestValidator : AbstractValidator<UpdateInvoiceStatusRequest>
{
    public UpdateInvoiceStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .IsInEnum()
            .WithMessage("Invalid invoice status.");
    }
}
