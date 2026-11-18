using FluentValidation;
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
    public static IRuleBuilderOptions<T, string> MustBeValidAssetId<T>(this IRuleBuilderOptions<T, string> ruleBuilder)
    {
        return ruleBuilder.Must(assetId =>
        {
            return !string.IsNullOrWhiteSpace(assetId) && assetId.Length >= 24;
        });
    }
}