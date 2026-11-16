using FluentValidation;
using Microsoft.Extensions.Options;
using UTTTGO.Api.Modules.Orders.Application;
using UTTTGO.Api.Modules.Orders.Contracts;

namespace UTTTGO.Api.Modules.Orders.Validators;

public sealed class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    private readonly OrderLimitsOptions _limits;

    public CreateOrderRequestValidator(IOptions<OrderLimitsOptions> limits)
    {
        _limits = limits.Value;
        RuleFor(request => request.ProductId).Must(value => value is not null &&
            value.Length == 24 && value.All(Uri.IsHexDigit));
        RuleFor(request => request.Quantity).Must(value => value is not null &&
            value >= 1 && value <= _limits.MaxQuantityPerOrder);
        RuleFor(request => request.Notes).Must(value => value is null ||
            value.Trim().Length <= 250);
        RuleFor(request => request.UnknownFields).Must(fields => fields is null || fields.Count == 0)
            .WithMessage("Campos no permitidos.");
    }
}
