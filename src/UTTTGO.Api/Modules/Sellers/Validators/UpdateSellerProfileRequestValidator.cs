using FluentValidation;
using UTTTGO.Api.Modules.Sellers.Contracts;

namespace UTTTGO.Api.Modules.Sellers.Validators;

public sealed class UpdateSellerProfileRequestValidator : AbstractValidator<UpdateSellerProfileRequest>
{
    public UpdateSellerProfileRequestValidator()
    {
        RuleFor(request => request.BusinessName).Must(value => value is not null &&
            value.Trim().Length is >= 1 and <= 100)
            .When(request => request.HasBusinessName);
        RuleFor(request => request.Description).Must(value => value is not null &&
            value.Trim().Length is >= 1 and <= 500)
            .When(request => request.HasDescription);
        RuleFor(request => request.PickupPoint).Must(value => value is not null &&
            value.Trim().Length is >= 1 and <= 160)
            .When(request => request.HasPickupPoint);
    }
}
