using FluentValidation;
using UTTTGO.Api.Modules.Orders.Application;
using UTTTGO.Api.Modules.Orders.Contracts;

namespace UTTTGO.Api.Modules.Orders.Validators;

public sealed class OrderListQueryValidator : AbstractValidator<OrderListQuery>
{
    public OrderListQueryValidator()
    {
        RuleFor(query => query.Page).Must(page => page >= 1 && page <= OrderListQuery.MaxPage);
        RuleFor(query => query.PageSize).Must(size => size >= 1 && size <= OrderListQuery.MaxPageSize);
        RuleFor(query => query.Status).Must(status => status is null || OrderStatus.IsKnown(status));
    }
}
