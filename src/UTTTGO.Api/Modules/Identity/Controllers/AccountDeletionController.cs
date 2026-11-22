using Microsoft.AspNetCore.Mvc;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Shared.Contracts;

namespace UTTTGO.Api.Modules.Identity.Controllers;

[ApiController]
[Route("api/v1/users")]
public sealed class AccountDeletionController(IDeleteOwnAccountService deletion) : ControllerBase
{
    [HttpDelete("me")]
    public async Task<IActionResult> DeleteMe(CancellationToken cancellationToken)
    {
        var token = BearerTokenReader.Read(Request);
        if (token is null)
            return Unauthorized(ApiError.Create(HttpContext, 401, "UNAUTHORIZED", "Sesión no autorizada."));

        var result = await deletion.DeleteAsync(token, cancellationToken);
        return result.Outcome switch
        {
            DeleteOwnAccountOutcome.Success => NoContent(),
            DeleteOwnAccountOutcome.Unauthorized => Unauthorized(ApiError.Create(HttpContext, 401,
                "UNAUTHORIZED", "Sesión no autorizada.")),
            DeleteOwnAccountOutcome.Conflict => Conflict(ApiError.Create(HttpContext, 409,
                "ACTIVE_ORDER_CONFLICT", "La cuenta tiene pedidos activos.")),
            _ => StatusCode(503, ApiError.Create(HttpContext, 503,
                "ACCOUNT_DELETION_UNAVAILABLE", "Servicio temporalmente no disponible."))
        };
    }
}
