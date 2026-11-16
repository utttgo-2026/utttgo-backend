using FluentValidation;
using UTTTGO.Api.Modules.Orders.Application;
using UTTTGO.Api.Modules.Orders.Contracts;

namespace UTTTGO.Api.Modules.Orders.Validators;

public sealed class UpdateOrderStatusRequestValidator : AbstractValidator<UpdateOrderStatusRequest>
{
    public UpdateOrderStatusRequestValidator()
    {
        RuleFor(request => request.Status).Must(value => value is not null && OrderStatus.IsKnown(value));
        RuleFor(request => request.Version).Must(value => value is not null && value >= 0);
        RuleFor(request => request.UnknownFields).Must(fields => fields is null || fields.Count == 0)
            .WithMessage("Campos no permitidos.");
    }
}
