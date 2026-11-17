using FluentValidation;
using UTTTGO.Api.Modules.Sellers.Contracts;

namespace UTTTGO.Api.Modules.Sellers.Validators;

public sealed class UpdateSellerProfileRequestValidator : AbstractValidator<UpdateSellerProfileRequest>
{
    public UpdateSellerProfileRequestValidator()
    {
        RuleFor(request => request.StoreName).Must(value => value is not null &&
            value.Trim().Length is >= 1 and <= 100)
            .When(request => request.HasStoreName);
        RuleFor(request => request.Description).Must(value => value is not null &&
            value.Trim().Length is >= 1 and <= 500)
            .When(request => request.HasDescription);
        RuleFor(request => request.Phone).Must(value => value is not null &&
            value.Trim().Length is >= 3 and <= 32)
            .When(request => request.HasPhone);
    }
}
