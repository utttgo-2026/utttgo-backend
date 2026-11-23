using FluentValidation;
using UTTTGO.Api.Modules.Reviews.Contracts;

namespace UTTTGO.Api.Modules.Reviews.Validators;

public sealed class CreateReviewRequestValidator : AbstractValidator<CreateReviewRequest>
{
    public CreateReviewRequestValidator()
    {
        RuleFor(request => request.Rating).NotNull().InclusiveBetween(1, 5);
        RuleFor(request => request.Comment).Must(comment => comment is null || comment.Trim().Length <= 1000);
    }
}

public sealed class SellerReviewsQueryValidator : AbstractValidator<SellerReviewsQuery>
{
    public SellerReviewsQueryValidator()
    {
        RuleFor(query => query.Page).InclusiveBetween(1, SellerReviewsQuery.MaxPage);
        RuleFor(query => query.PageSize).InclusiveBetween(1, SellerReviewsQuery.MaxPageSize);
    }
}
