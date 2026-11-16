using FluentValidation;
using UTTTGO.Api.Modules.Catalog.Contracts;

namespace UTTTGO.Api.Modules.Catalog.Validators;

public sealed class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(request => request.Name).Must(value => value is not null &&
            value.Trim().Length is >= 1 and <= 120);
        RuleFor(request => request.Description).Must(value => value is not null &&
            value.Trim().Length is >= 1 and <= 500);
        RuleFor(request => request.Ingredients).Must(value => value is null ||
            value.Count <= 20 && value.All(item => item is not null &&
                item.Trim().Length is >= 1 and <= 60));
        RuleFor(request => request.CategoryId).Must(value => value is not null &&
            value.Length == 24 && value.All(Uri.IsHexDigit));
        RuleFor(request => request.Price).Must(value => value is not null && value > 0m && value <= 1_000_000m);
        RuleFor(request => request.Stock).Must(value => value is not null && value >= 0 && value <= 1_000_000);
        RuleFor(request => request.ImageUrl).Must(value => value is null || value.Trim().Length <= 2048);
        RuleFor(request => request.UnknownFields).Must(fields => fields is null || fields.Count == 0)
            .WithMessage("Campos no permitidos.");
    }
}
