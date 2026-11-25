using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using UTTTGO.Api.Modules.Admin.Application;
using UTTTGO.Api.Modules.Admin.Contracts;
using UTTTGO.Api.Shared.Contracts;

namespace UTTTGO.Api.Modules.Admin.Controllers;

[ApiController]
[Route("api/v1/admin/users")]
public sealed class AdminUsersStatusController(IAdminUserStatusService service,
    IValidator<AdminUserStatusRequest> validator) : ControllerBase
{
    [HttpPatch("{userId}/status")]
    [ProducesResponseType(typeof(AdminUserStatusResponse), 200)]
    [ProducesResponseType(typeof(ApiError), 400)]
    [ProducesResponseType(typeof(ApiError), 401)]
    [ProducesResponseType(typeof(ApiError), 403)]
    [ProducesResponseType(typeof(ApiError), 404)]
    [ProducesResponseType(typeof(ApiError), 409)]
    [ProducesResponseType(typeof(ApiError), 429)]
    [ProducesResponseType(typeof(ApiError), 503)]
    public async Task<IActionResult> SetStatus(string userId, [FromBody] AdminUserStatusRequest? request,
        CancellationToken cancellationToken)
    {
        var token = BearerTokenReader.Read(Request);
        if (token is null) return Unauthorized(ApiError.Create(HttpContext, 401, "UNAUTHORIZED", "Sesión no autorizada."));
        if (request is null || !(await validator.ValidateAsync(request, cancellationToken)).IsValid)
            return BadRequest(ApiError.Create(HttpContext, 400, "VALIDATION_ERROR", "Solicitud inválida."));
        var result = await service.SetAsync(token, userId, request.AccountStatus!, HttpContext.TraceIdentifier, cancellationToken);
        return result.Outcome switch
        {
            AdminUserStatusOutcome.Success when result.Response is not null => Ok(result.Response),
            AdminUserStatusOutcome.Unauthorized => Unauthorized(ApiError.Create(HttpContext, 401, "UNAUTHORIZED", "Sesión no autorizada.")),
            AdminUserStatusOutcome.Forbidden => StatusCode(403, ApiError.Create(HttpContext, 403, "FORBIDDEN", "Acceso denegado.")),
            AdminUserStatusOutcome.SelfTarget => Conflict(ApiError.Create(HttpContext, 409, "ADMIN_SELF_STATUS_CHANGE_FORBIDDEN", "No puedes cambiar tu propio estado.")),
            AdminUserStatusOutcome.NotFound => NotFound(ApiError.Create(HttpContext, 404, "USER_NOT_FOUND", "Usuario no encontrado.")),
            AdminUserStatusOutcome.Unchanged => Ok(result.Response),
            AdminUserStatusOutcome.RateLimited => StatusCode(429, ApiError.Create(HttpContext, 429, "RATE_LIMIT_EXCEEDED", "Intenta de nuevo más tarde.")),
            _ => StatusCode(503, ApiError.Create(HttpContext, 503, "DEPENDENCY_UNAVAILABLE", "Servicio temporalmente no disponible."))
        };
    }
}
