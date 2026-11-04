using FluentValidation;
using UTTTGO.Api.Modules.Identity.Contracts;

namespace UTTTGO.Api.Modules.Identity.Validators;

public sealed class UpdateOwnProfileRequestValidator : AbstractValidator<UpdateOwnProfileRequest>
{
    public UpdateOwnProfileRequestValidator()
    {
        // Local candidate limits; the final SDD has not approved lengths or phone format.
        RuleFor(request => request.DisplayName).Must(value => value is not null &&
            value.Trim().Length is >= 1 and <= 100)
            .When(request => request.HasDisplayName);
        RuleFor(request => request.Phone).Must(value => value is not null &&
            System.Text.RegularExpressions.Regex.IsMatch(value, @"^\+?[0-9]{10,15}$"))
            .When(request => request.HasPhone);
    }
}
