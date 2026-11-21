using FluentValidation;
using UTTTGO.Api.Modules.Catalog.Contracts;

namespace UTTTGO.Api.Modules.Catalog.Validators;

public sealed class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(request => request.Name).Must((request, value) => !request.HasName ||
            value is not null && value.Trim().Length is >= 1 and <= 120);
        RuleFor(request => request.Description).Must((request, value) => !request.HasDescription ||
            value is not null && value.Trim().Length is >= 1 and <= 500);
        RuleFor(request => request.Ingredients).Must((request, value) => !request.HasIngredients ||
            value is not null && value.Count <= 20 && value.All(item => item is not null &&
                item.Trim().Length is >= 1 and <= 60));
        RuleFor(request => request.Price).Must((request, value) => !request.HasPrice ||
            value is not null && value > 0m && value <= 1_000_000m);
        RuleFor(request => request.Stock).Must((request, value) => !request.HasStock ||
            value is not null && value >= 0 && value <= 1_000_000);
        RuleFor(request => request.MediaAssetId).Must((request, value) => !request.HasMediaAssetId ||
            value is null || value.Length == 24 && value.All(Uri.IsHexDigit));
    }
}
