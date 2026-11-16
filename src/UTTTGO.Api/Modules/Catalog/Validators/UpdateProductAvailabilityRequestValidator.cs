using FluentValidation;
using UTTTGO.Api.Modules.Catalog.Application;
using UTTTGO.Api.Modules.Catalog.Contracts;

namespace UTTTGO.Api.Modules.Catalog.Validators;

public sealed class UpdateProductAvailabilityRequestValidator
    : AbstractValidator<UpdateProductAvailabilityRequest>
{
    public UpdateProductAvailabilityRequestValidator()
    {
        RuleFor(request => request.Status).Must(value => value is not null &&
            (value == ProductAvailability.Active || value == ProductAvailability.Paused));
        RuleFor(request => request.UnknownFields).Must(fields => fields is null || fields.Count == 0)
            .WithMessage("Campos no permitidos.");
    }
}
