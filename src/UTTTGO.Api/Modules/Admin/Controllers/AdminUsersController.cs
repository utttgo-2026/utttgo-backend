using System.Globalization;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using UTTTGO.Api.Modules.Admin.Application;
using UTTTGO.Api.Modules.Admin.Contracts;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Identity.Contracts;

namespace UTTTGO.Api.Modules.Admin.Controllers;

[ApiController]
[Route("api/v1/admin/users")]
public sealed class AdminUsersController(IAdminUsersService users,
    IValidator<AdminUsersQueryRequest> validator) : ControllerBase
{
    private static readonly string[] AllowedKeys =
        ["search", "role", "accountStatus", "page", "pageSize", "sortBy"];

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var authorization = Request.Headers.Authorization;
        if (authorization.Count != 1 || authorization[0] is not string header ||
            !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ||
            header.Length <= 7 || header[7..].Trim() != header[7..] ||
            header[7..].Any(char.IsWhiteSpace))
            return Unauthorized(ApiError.Create(HttpContext, 401, "UNAUTHORIZED", "Sesión no autorizada."));

        if (Request.Query.Any(pair => !AllowedKeys.Contains(pair.Key, StringComparer.Ordinal) ||
                pair.Value.Count != 1))
            return BadRequest(ApiError.Create(HttpContext, 400, "VALIDATION_ERROR", "Solicitud inválida."));

        var request = new AdminUsersQueryRequest(Request.Query["search"], Request.Query["role"],
            Request.Query["accountStatus"], Request.Query["page"], Request.Query["pageSize"],
            Request.Query["sortBy"]);
        if (!(await validator.ValidateAsync(request, cancellationToken)).IsValid)
            return BadRequest(ApiError.Create(HttpContext, 400, "VALIDATION_ERROR", "Solicitud inválida."));

        var query = new AdminUserListQuery(request.Search?.Trim().Normalize(NormalizationForm.FormKC),
            request.Role, request.AccountStatus, int.Parse(request.Page!, CultureInfo.InvariantCulture),
            int.Parse(request.PageSize!, CultureInfo.InvariantCulture), request.SortBy != "createdAtAsc");
        var result = await users.ListAsync(header[7..], query, HttpContext.TraceIdentifier, cancellationToken);
        return result.Outcome switch
        {
            AdminUsersOutcome.Success when result.Response is not null => Ok(result.Response),
            AdminUsersOutcome.Unauthorized => Unauthorized(ApiError.Create(HttpContext, 401,
                "UNAUTHORIZED", "Sesión no autorizada.")),
            AdminUsersOutcome.Forbidden => StatusCode(403, ApiError.Create(HttpContext, 403,
                "FORBIDDEN", "Acceso denegado.")),
            AdminUsersOutcome.RateLimited => StatusCode(429, ApiError.Create(HttpContext, 429,
                "RATE_LIMITED", "Intenta de nuevo más tarde.")),
            _ => StatusCode(503, ApiError.Create(HttpContext, 503,
                "ADMIN_USERS_UNAVAILABLE", "Servicio temporalmente no disponible."))
        };
    }
}
