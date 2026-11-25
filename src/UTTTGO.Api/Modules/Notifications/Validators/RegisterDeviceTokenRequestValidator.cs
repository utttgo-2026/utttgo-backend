using FluentValidation;
using UTTTGO.Api.Modules.Notifications.Contracts;

namespace UTTTGO.Api.Modules.Notifications.Validators;

public sealed class RegisterDeviceTokenRequestValidator : AbstractValidator<RegisterDeviceTokenRequest>
{
    public RegisterDeviceTokenRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(4096);
        RuleFor(x => x.InstallationId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Platform).Must(x => x is "android" or "ios");
        RuleFor(x => x.AppVersion).MaximumLength(32);
    }
}
