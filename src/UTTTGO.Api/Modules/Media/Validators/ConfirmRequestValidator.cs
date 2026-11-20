using FluentValidation;
using MongoDB.Bson;
using UTTTGO.Api.Modules.Media.Contracts;

namespace UTTTGO.Api.Modules.Media.Validators;

public class ConfirmRequestValidator : AbstractValidator<ConfirmRequest>
{
    public ConfirmRequestValidator()
    {
        RuleFor(x => x.MediaAssetId)
            .Must(id => ObjectId.TryParse(id, out _)).WithMessage("MediaAssetId inválido");
    }
}
