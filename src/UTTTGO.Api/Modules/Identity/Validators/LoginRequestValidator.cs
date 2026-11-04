using FluentValidation;
using UTTTGO.Api.Modules.Identity.Contracts;

namespace UTTTGO.Api.Modules.Identity.Validators;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress();
        RuleFor(request => request.Password).NotEmpty();
        RuleFor(request => request.UnknownFields).Must(fields => fields is null || fields.Count == 0)
            .WithMessage("Campos no permitidos.");
    }
}
