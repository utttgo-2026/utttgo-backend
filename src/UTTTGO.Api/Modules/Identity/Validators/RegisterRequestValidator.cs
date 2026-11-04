using FluentValidation;
using UTTTGO.Api.Modules.Identity.Contracts;

namespace UTTTGO.Api.Modules.Identity.Validators;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress();
        RuleFor(request => request.Password).NotEmpty();
        RuleFor(request => request.DisplayName).NotEmpty();
        RuleFor(request => request.UnknownFields).Must(fields => fields is null || fields.Count == 0)
            .WithMessage("Campos no permitidos.");
    }

    public static bool HasInstitutionalDomain(string email)
    {
        var at = email.LastIndexOf('@');
        return at > 0 && email.IndexOf('@') == at &&
            string.Equals(email[(at + 1)..], "uttt.edu.mx", StringComparison.OrdinalIgnoreCase);
    }
}
