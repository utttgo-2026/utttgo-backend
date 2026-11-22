using Microsoft.AspNetCore.Mvc;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Shared.Contracts;

namespace UTTTGO.Api.Modules.Identity.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class LogoutController(ILogoutService logout) : ControllerBase
{
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var token = BearerTokenReader.Read(Request);
        if (token is null)
            return Unauthorized(ApiError.Create(HttpContext, 401, "UNAUTHORIZED", "Sesión no autorizada."));

        var result = await logout.LogoutAsync(token, cancellationToken);
        return result.Outcome switch
        {
            LogoutOutcome.Success => NoContent(),
            LogoutOutcome.Unauthorized => Unauthorized(ApiError.Create(HttpContext, 401,
                "UNAUTHORIZED", "Sesión no autorizada.")),
            _ => StatusCode(503, ApiError.Create(HttpContext, 503,
                "AUTH_UNAVAILABLE", "Servicio temporalmente no disponible."))
        };
    }
}
