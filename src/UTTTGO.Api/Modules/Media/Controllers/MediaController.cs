using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Media.Application;
using UTTTGO.Api.Modules.Media.Contracts;
using UTTTGO.Api.Shared.Contracts;
using UTTTGO.Api.Shared.Infrastructure;

namespace UTTTGO.Api.Modules.Media.Controllers;

[ApiController]
[Route("api/v1/media")]
public sealed class MediaController(MediaService media, IAuthenticatedLocalProfileReader profiles,
    RedisRateLimiter limiter, IValidator<PresignedUrlRequest> validator,
    IValidator<ConfirmRequest> confirmValidator) : ControllerBase
{
    private async Task<(string? Owner, IActionResult? Error)> AuthorizeAsync(CancellationToken ct)
    {
        var token = BearerTokenReader.Read(Request);
        if (token is null) return (null, Unauthorized(ApiError.Create(HttpContext, 401,
            "UNAUTHORIZED", "Sesión no autorizada.")));
        var result = await profiles.ReadAsync(token, ct);
        if (result.Outcome is AuthenticatedLocalProfileOutcome.Unauthorized
            or AuthenticatedLocalProfileOutcome.ProfileMissing)
            return (null, Unauthorized(ApiError.Create(HttpContext, 401,
                "UNAUTHORIZED", "Sesión no autorizada.")));
        if (result.Outcome != AuthenticatedLocalProfileOutcome.Success || result.Profile is null)
            return (null, StatusCode(503, ApiError.Create(HttpContext, 503,
                "MEDIA_UNAVAILABLE", "Servicio temporalmente no disponible.")));
        if (result.Profile.AccountStatus != "Active" ||
            !result.Profile.Roles.Contains("Seller", StringComparer.Ordinal) ||
            string.IsNullOrWhiteSpace(result.Profile.SellerId))
            return (null, StatusCode(403, ApiError.Create(HttpContext, 403,
                "SELLER_ROLE_REQUIRED", "Se requiere vendedor activo.")));
        return (result.Profile.Id, null);
    }

    [HttpPost("upload-url")]
    [ProducesResponseType(typeof(PresignedUrlResponse), 201)]
    [ProducesResponseType(typeof(ApiError), 400)]
    [ProducesResponseType(typeof(ApiError), 401)]
    [ProducesResponseType(typeof(ApiError), 403)]
    [ProducesResponseType(typeof(ApiError), 429)]
    [ProducesResponseType(typeof(ApiError), 503)]
    public async Task<IActionResult> UploadUrl([FromBody] PresignedUrlRequest request, CancellationToken ct)
    {
        try
        {
            var auth = await AuthorizeAsync(ct);
            if (auth.Error is not null) return auth.Error;
            if (!(await validator.ValidateAsync(request, ct)).IsValid)
                return BadRequest(ApiError.Create(HttpContext, 400, "VALIDATION_ERROR", "Solicitud inválida."));
            if (!await limiter.IsAllowedAsync(("media-upload", auth.Owner!)))
                return StatusCode(429, ApiError.Create(HttpContext, 429, "RATE_LIMITED", "Intenta más tarde."));
            return StatusCode(201, await media.StartAsync(auth.Owner!, request, ct));
        }
        catch (ArgumentException) { return BadRequest(ApiError.Create(HttpContext, 400, "VALIDATION_ERROR", "Solicitud inválida.")); }
        catch (Exception) when (!ct.IsCancellationRequested) { return Unavailable(); }
    }

    [HttpPost("{mediaAssetId}/complete")]
    [ProducesResponseType(typeof(ConfirmResponse), 200)]
    [ProducesResponseType(typeof(ApiError), 400)]
    [ProducesResponseType(typeof(ApiError), 401)]
    [ProducesResponseType(typeof(ApiError), 404)]
    [ProducesResponseType(typeof(ApiError), 429)]
    [ProducesResponseType(typeof(ApiError), 503)]
    public async Task<IActionResult> Complete(string mediaAssetId, CancellationToken ct)
    {
        try
        {
            var auth = await AuthorizeAsync(ct);
            if (auth.Error is not null) return auth.Error;
            if (!await limiter.IsAllowedAsync(("media-confirm", auth.Owner!)))
                return StatusCode(429, ApiError.Create(HttpContext, 429, "RATE_LIMITED", "Intenta más tarde."));
            var result = await media.ConfirmAsync(mediaAssetId, auth.Owner!, ct);
            return result is null ? NotFound(ApiError.Create(HttpContext, 404, "ASSET_NOT_FOUND",
                "Asset no encontrado.")) : Ok(result);
        }
        catch (ArgumentException) { return BadRequest(ApiError.Create(HttpContext, 400, "INVALID_OBJECT", "Objeto inválido.")); }
        catch (Exception) when (!ct.IsCancellationRequested) { return Unavailable(); }
    }

    // Compatibility route; both routes share the same authorization, rate limit and CAS transition.
    [HttpPost("confirm")]
    [ProducesResponseType(typeof(ConfirmResponse), 200)]
    [ProducesResponseType(typeof(ApiError), 400)]
    [ProducesResponseType(typeof(ApiError), 401)]
    [ProducesResponseType(typeof(ApiError), 403)]
    [ProducesResponseType(typeof(ApiError), 404)]
    [ProducesResponseType(typeof(ApiError), 429)]
    [ProducesResponseType(typeof(ApiError), 503)]
    public async Task<IActionResult> Confirm([FromBody] ConfirmRequest request, CancellationToken ct)
    {
        if (!(await confirmValidator.ValidateAsync(request, ct)).IsValid)
            return BadRequest(ApiError.Create(HttpContext, 400, "VALIDATION_ERROR", "Solicitud inválida."));
        return await Complete(request.MediaAssetId, ct);
    }

    [HttpGet("{mediaAssetId}")]
    [ProducesResponseType(typeof(MediaAssetResponse), 200)]
    [ProducesResponseType(typeof(ApiError), 401)]
    [ProducesResponseType(typeof(ApiError), 404)]
    [ProducesResponseType(typeof(ApiError), 503)]
    public async Task<IActionResult> Get(string mediaAssetId, CancellationToken ct)
    {
        try
        {
            var auth = await AuthorizeAsync(ct);
            if (auth.Error is not null) return auth.Error;
            if (!await limiter.IsAllowedAsync(("media-confirm", auth.Owner!)))
                return StatusCode(429, ApiError.Create(HttpContext, 429, "RATE_LIMITED", "Intenta más tarde."));
            var asset = await media.ReadOwnedAsync(mediaAssetId, auth.Owner!, ct);
            if (asset is null) return NotFound(ApiError.Create(HttpContext, 404,
                "ASSET_NOT_FOUND", "Asset no encontrado."));
            var url = asset.Status == MediaStatus.Approved
                ? await media.GetApprovedReadUrlAsync(asset.Id, ct) : null;
            return Ok(new MediaAssetResponse(asset.Id, asset.Status, url?.Url, url?.ExpiresAt));
        }
        catch (Exception) when (!ct.IsCancellationRequested) { return Unavailable(); }
    }

    private IActionResult Unavailable() => StatusCode(503, ApiError.Create(HttpContext, 503,
        "MEDIA_UNAVAILABLE", "Servicio temporalmente no disponible."));
}
