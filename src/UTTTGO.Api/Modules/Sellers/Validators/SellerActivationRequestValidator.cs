using FluentValidation;
using UTTTGO.Api.Modules.Sellers.Contracts;

namespace UTTTGO.Api.Modules.Sellers.Validators;

public sealed class SellerActivationRequestValidator : AbstractValidator<SellerActivationRequest>
{
    public SellerActivationRequestValidator()
    {
        RuleFor(request => request.StoreName).Must(value => value is not null &&
            value.Trim().Length is >= 1 and <= 100);
        RuleFor(request => request.Description).Must(value => value is not null &&
            value.Trim().Length is >= 1 and <= 500);
        RuleFor(request => request.Phone).Must(value => value is not null &&
            value.Trim().Length is >= 3 and <= 32);
        RuleFor(request => request.UnknownFields).Must(fields => fields is null || fields.Count == 0)
            .WithMessage("Campos no permitidos.");
    }
}
