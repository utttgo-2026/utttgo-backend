using FluentValidation;
using UTTTGO.Api.Modules.Catalog.Contracts;

namespace UTTTGO.Api.Modules.Catalog.Validators;

public sealed class ProductListQueryValidator : AbstractValidator<ProductListQuery>
{
    public ProductListQueryValidator()
    {
        RuleFor(query => query.Search).Must(value => value is null ||
            value.Trim().Length is >= 1 and <= ProductListQuery.MaxSearchLength);
        RuleFor(query => query.CategoryId).Must(value => value is null ||
            value.Length == 24 && value.All(Uri.IsHexDigit));
        RuleFor(query => query.MinPrice).Must(value => value is null || value >= 0m);
        RuleFor(query => query.MaxPrice).Must(value => value is null || value >= 0m);
        RuleFor(query => query).Must(query => query.MinPrice is null || query.MaxPrice is null ||
            query.MaxPrice >= query.MinPrice);
        RuleFor(query => query.Page).Must(page => page >= 1 && page <= ProductListQuery.MaxPage);
        RuleFor(query => query.PageSize).Must(size => size >= 1 && size <= ProductListQuery.MaxPageSize);
        RuleFor(query => query.SortBy).Must(ProductSort.IsAllowed);
    }
}
