using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Identity.Contracts;

namespace UTTTGO.Api.Modules.Identity.Controllers;

[ApiController]
[Route("api/v1/users")]
public sealed class UsersController(IOwnProfileService profiles, IUpdateOwnProfileService updates,
    IValidator<UpdateOwnProfileRequest> validator) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var token = BearerTokenReader.Read(Request);
        if (token is null)
            return Unauthorized(ApiError.Create(HttpContext, 401, "UNAUTHORIZED", "Sesión no autorizada."));

        var result = await profiles.GetAsync(token, cancellationToken);
        return result.Outcome switch
        {
            SessionOutcome.Success when result.Response is not null => Ok(result.Response),
            SessionOutcome.Unauthorized or SessionOutcome.ProfileMissing => Unauthorized(ApiError.Create(
                HttpContext, 401, "UNAUTHORIZED", "Sesión no autorizada.")),
            _ => StatusCode(503, ApiError.Create(HttpContext, 503,
                "AUTH_UNAVAILABLE", "Servicio temporalmente no disponible."))
        };
    }

    [HttpPatch("me")]
    public async Task<IActionResult> UpdateMe([FromBody] JsonElement json, CancellationToken cancellationToken)
    {
        var token = BearerTokenReader.Read(Request);
        if (token is null)
            return Unauthorized(ApiError.Create(HttpContext, 401, "UNAUTHORIZED", "Sesión no autorizada."));

        if (!UpdateOwnProfileRequest.TryParse(json, out var request) || request is null ||
            !(await validator.ValidateAsync(request, cancellationToken)).IsValid)
            return BadRequest(ApiError.Create(HttpContext, 400, "VALIDATION_ERROR", "Solicitud inválida."));

        var result = await updates.UpdateAsync(token, request, cancellationToken);
        return result.Outcome switch
        {
            UpdateOwnProfileOutcome.Success when result.Response is not null => Ok(result.Response),
            UpdateOwnProfileOutcome.Unauthorized => Unauthorized(ApiError.Create(HttpContext, 401,
                "UNAUTHORIZED", "Sesión no autorizada.")),
            UpdateOwnProfileOutcome.Conflict => Conflict(ApiError.Create(HttpContext, 409,
                "PROFILE_CONFLICT", "El perfil cambió; vuelve a intentarlo.")),
            _ => StatusCode(503, ApiError.Create(HttpContext, 503,
                "PROFILE_UNAVAILABLE", "Servicio temporalmente no disponible."))
        };
    }
}
