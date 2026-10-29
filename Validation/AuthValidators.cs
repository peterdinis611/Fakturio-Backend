using Fakturio.Api.Dtos;
using FluentValidation;

namespace Fakturio.Api.Validation;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("E-mail je povinný.")
            .EmailAddress().WithMessage("Neplatný e-mail.")
            .MaximumLength(200);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Heslo je povinné.")
            .MinimumLength(8).WithMessage("Heslo musí mať aspoň 8 znakov.")
            .MaximumLength(128)
            .Matches("[A-ZÁÄČĎÉÍĹĽŇÓÔŔŠŤÚÝŽ]")
            .WithMessage("Heslo musí obsahovať veľké písmeno.")
            .Matches("[a-záäčďéíĺľňóôŕšťúýž]")
            .WithMessage("Heslo musí obsahovať malé písmeno.")
            .Matches(@"[^A-Za-zÁÄČĎÉÍĹĽŇÓÔŔŠŤÚÝŽáäčďéíĺľňóôŕšťúýž0-9]")
            .WithMessage("Heslo musí obsahovať špeciálny znak.");

        RuleFor(x => x.FirstName).MaximumLength(100);
        RuleFor(x => x.LastName).MaximumLength(100);
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("E-mail je povinný.")
            .EmailAddress().WithMessage("Neplatný e-mail.")
            .MaximumLength(200);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Heslo je povinné.")
            .MaximumLength(128);
    }
}
