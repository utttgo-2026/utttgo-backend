using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using UTTTGO.Api.Modules.Notifications.Application;
using UTTTGO.Api.Modules.Notifications.Contracts;
using UTTTGO.Api.Shared.Contracts;
namespace UTTTGO.Api.Modules.Notifications.Controllers;

[ApiController, Route("api/v1/notifications")]
public sealed class NotificationsController(IDeviceTokenService service, IValidator<RegisterDeviceTokenRequest> validator) : ControllerBase
{
    [HttpPost("device-tokens")]
    [ProducesResponseType(typeof(DeviceTokenResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(DeviceTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Register(RegisterDeviceTokenRequest request, CancellationToken ct)
    {
        var bearer = BearerTokenReader.Read(Request); if (bearer is null) return Unauthorized(ApiError.Create(HttpContext, 401, "UNAUTHORIZED", "Sesión no autorizada."));
        if (!(await validator.ValidateAsync(request, ct)).IsValid) return BadRequest(ApiError.Create(HttpContext, 400, "VALIDATION_ERROR", "Solicitud inválida."));
        var r = await service.RegisterAsync(bearer, request, ct);
        return r.Outcome switch { DeviceTokenOutcome.Created => StatusCode(201, r.Response), DeviceTokenOutcome.Updated => Ok(r.Response), DeviceTokenOutcome.Unauthorized => Unauthorized(ApiError.Create(HttpContext, 401, "UNAUTHORIZED", "Sesión no autorizada.")), DeviceTokenOutcome.NotActive => StatusCode(403, ApiError.Create(HttpContext, 403, "ACCOUNT_NOT_ACTIVE", "Cuenta no activa.")), DeviceTokenOutcome.RateLimited => StatusCode(429, ApiError.Create(HttpContext, 429, "RATE_LIMITED", "Intenta de nuevo más tarde.")), _ => StatusCode(503, ApiError.Create(HttpContext, 503, "NOTIFICATION_UNAVAILABLE", "Servicio temporalmente no disponible.")) };
    }
    [HttpDelete("device-tokens/{deviceTokenId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Remove(string deviceTokenId, CancellationToken ct)
    {
        var bearer = BearerTokenReader.Read(Request); if (bearer is null) return Unauthorized(ApiError.Create(HttpContext, 401, "UNAUTHORIZED", "Sesión no autorizada."));
        var r = await service.RemoveAsync(bearer, deviceTokenId, ct);
        return r switch { DeviceTokenOutcome.Removed => NoContent(), DeviceTokenOutcome.Unauthorized => Unauthorized(ApiError.Create(HttpContext, 401, "UNAUTHORIZED", "Sesión no autorizada.")), DeviceTokenOutcome.NotActive => StatusCode(403, ApiError.Create(HttpContext, 403, "ACCOUNT_NOT_ACTIVE", "Cuenta no activa.")), DeviceTokenOutcome.NotFound => NotFound(ApiError.Create(HttpContext, 404, "DEVICE_TOKEN_NOT_FOUND", "Dispositivo no disponible.")), _ => StatusCode(503, ApiError.Create(HttpContext, 503, "NOTIFICATION_UNAVAILABLE", "Servicio temporalmente no disponible.")) };
    }
}
