using FluentValidation;
using UTTTGO.Api.Modules.Sellers.Contracts;

namespace UTTTGO.Api.Modules.Sellers.Validators;

public sealed class SellerIdRequestValidator : AbstractValidator<SellerIdRequest>
{
    public SellerIdRequestValidator()
    {
        // Local candidate: Mongo ObjectId as a 24-character hexadecimal string.
        RuleFor(request => request.SellerId).Must(id => id is not null &&
            id.Length == 24 && id.All(Uri.IsHexDigit));
    }
}
