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

public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Neplatný e-mail.")
            .MaximumLength(200)
            .When(x => x.Email is not null);

        RuleFor(x => x.FirstName).MaximumLength(100);
        RuleFor(x => x.LastName).MaximumLength(100);
        RuleFor(x => x.ImageUrl)
            .MaximumLength(500)
            .Must(BeValidUrl)
            .When(x => !string.IsNullOrWhiteSpace(x.ImageUrl))
            .WithMessage("ImageUrl must be a valid http(s) URL.");

        RuleFor(x => x)
            .Must(HasAtLeastOneField)
            .WithMessage("Aspoň jedno pole musí byť vyplnené.");
    }

    private static bool HasAtLeastOneField(UpdateUserRequest x) =>
        x.FirstName is not null
        || x.LastName is not null
        || x.ImageUrl is not null
        || x.Email is not null;

    private static bool BeValidUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Aktuálne heslo je povinné.")
            .MaximumLength(128);

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Nové heslo je povinné.")
            .MinimumLength(8).WithMessage("Heslo musí mať aspoň 8 znakov.")
            .MaximumLength(128)
            .Matches("[A-ZÁÄČĎÉÍĹĽŇÓÔŔŠŤÚÝŽ]")
            .WithMessage("Heslo musí obsahovať veľké písmeno.")
            .Matches("[a-záäčďéíĺľňóôŕšťúýž]")
            .WithMessage("Heslo musí obsahovať malé písmeno.")
            .Matches(@"[^A-Za-zÁÄČĎÉÍĹĽŇÓÔŔŠŤÚÝŽáäčďéíĺľňóôŕšťúýž0-9]")
            .WithMessage("Heslo musí obsahovať špeciálny znak.")
            .NotEqual(x => x.CurrentPassword)
            .WithMessage("Nové heslo musí byť iné ako aktuálne.");
    }
}
