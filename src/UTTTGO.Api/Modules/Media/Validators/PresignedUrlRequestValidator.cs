using FluentValidation;
using UTTTGO.Api.Modules.Media.Contracts;
using UTTTGO.Api.Modules.Media.Application;

namespace UTTTGO.Api.Modules.Media.Validators;

public class PresignedUrlRequestValidator : AbstractValidator<PresignedUrlRequest>
{
    public PresignedUrlRequestValidator()
    {
        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("El nombre de archivo es obligatorio")
            .MaximumLength(200).WithMessage("El nombre de archivo es muy largo");

        RuleFor(x => x.MimeType)
            .NotEmpty().WithMessage("El tipo MIME es obligatorio")
            .MustBeValidImageMime().WithMessage("Tipo MIME no permitido. Use: image/jpeg, image/png, image/webp");

        RuleFor(x => x.Size)
            .GreaterThan(0).WithMessage("El tamaño debe ser mayor a 0")
            .LessThanOrEqualTo(MediaImagePolicy.MaxBytes).WithMessage("El tamaño máximo es 4 MB");
    }
}

public static class MimeTypeValidatorExtensions
{
    public static IRuleBuilderOptions<T, string> MustBeValidImageMime<T>(this IRuleBuilderOptions<T, string> ruleBuilder)
    {
        return ruleBuilder.Must(mimeType =>
        {
            var allowed = new[] { "image/jpeg", "image/png", "image/webp" };
            return allowed.Contains(mimeType, StringComparer.OrdinalIgnoreCase);
        });
    }
}
