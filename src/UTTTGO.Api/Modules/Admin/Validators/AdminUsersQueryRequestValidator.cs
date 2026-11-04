using System.Globalization;
using System.Text;
using FluentValidation;
using UTTTGO.Api.Modules.Admin.Contracts;

namespace UTTTGO.Api.Modules.Admin.Validators;

public sealed class AdminUsersQueryRequestValidator : AbstractValidator<AdminUsersQueryRequest>
{
    public const int InitialMaxPageSize = 50;

    public AdminUsersQueryRequestValidator(IConfiguration configuration)
    {
        var max = configuration.GetValue<int?>("AdminUsers:MaxPageSize") ?? InitialMaxPageSize;
        // Configuration may tighten but must not silently loosen the ratified initial ceiling.
        RuleFor(request => request.PageSize).Must(value =>
            int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var size) &&
            size >= 1 && size <= max && max is >= 1 and <= InitialMaxPageSize);
        RuleFor(request => request.Page).Must(value =>
            int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var page) && page >= 1);
        RuleFor(request => request).Must(request =>
            !int.TryParse(request.Page, NumberStyles.None, CultureInfo.InvariantCulture, out var page) ||
            !int.TryParse(request.PageSize, NumberStyles.None, CultureInfo.InvariantCulture, out var size) ||
            ((long)page - 1) * size <= int.MaxValue);
        RuleFor(request => request.Search).Must(value => value is null ||
            !string.IsNullOrWhiteSpace(value) && value.Trim().Normalize(NormalizationForm.FormKC).Length <= 100);
        RuleFor(request => request.Role).Must(value => value is null or "Buyer" or "Seller" or "Admin");
        RuleFor(request => request.AccountStatus).Must(value =>
            value is null or "Active" or "PendingEmailVerification" or "Suspended");
        RuleFor(request => request.SortBy).Must(value =>
            value is null or "createdAtDesc" or "createdAtAsc");
    }
}
