using FluentValidation;
using UTTTGO.Api.Modules.Orders.Contracts;

namespace UTTTGO.Api.Modules.Orders.Validators;

public sealed class DeliveryConfirmationRequestValidator : AbstractValidator<DeliveryConfirmationRequest>
{
    public DeliveryConfirmationRequestValidator()
    {
        RuleFor(request => request.Token).Must(value => value is null || value.Trim().Length <= 200);
        RuleFor(request => request.UnknownFields).Must(fields => fields is null || fields.Count == 0)
            .WithMessage("Campos no permitidos.");
    }
}
