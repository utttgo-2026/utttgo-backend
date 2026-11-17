using FluentValidator;
using UTTTGO.Api.Modules.Media.Contracts;

namespace UTTTGO.Api.Modules.Media.Validators;

public class ConfirmRequestValidator : AbstractValidator<ConfirmRequest>
{
    public ConfirmRequestValidator()
    {
        RuleFor(x => x.AssetId)
            .NotEmpty().WithMessage("El assetId es obligatorio")
            .MustBeValidAssetId().WithMessage("AssetId inválido");
    }
}

public static class ConfirmRequestExtensions
{
    public static bool MustBeValidAssetId(this string assetId)
    {
        // Validación básica: debe ser un GUID o tener un formato válido
        return !string.IsNullOrWhiteSpace(assetId) && assetId.Length >= 24;
    }
}