using FluentValidation;
using UTTTGO.Api.Modules.Admin.Contracts;

namespace UTTTGO.Api.Modules.Admin.Validators;

public sealed class AdminUserStatusRequestValidator : AbstractValidator<AdminUserStatusRequest>
{
    public AdminUserStatusRequestValidator()
    {
        RuleFor(request => request.AccountStatus)
            .Must(status => status is "Active" or "Suspended")
            .WithErrorCode("VALIDATION_ERROR");
        RuleFor(request => request.AdditionalProperties)
            .Must(properties => properties is null or { Count: 0 })
            .WithErrorCode("VALIDATION_ERROR");
    }
}
