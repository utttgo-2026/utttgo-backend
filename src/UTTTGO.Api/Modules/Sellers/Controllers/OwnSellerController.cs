using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using UTTTGO.Api.Modules.Sellers.Application;
using UTTTGO.Api.Modules.Sellers.Contracts;
using UTTTGO.Api.Modules.Sellers.Validators;
using UTTTGO.Api.Shared.Contracts;

namespace UTTTGO.Api.Modules.Sellers.Controllers;

[ApiController]
[Route("api/v1/sellers")]
public sealed class OwnSellerController(ISellerActivationService activation,
    IValidator<SellerActivationRequest> activationValidator, IOwnSellerProfileService ownProfile,
    IUpdateOwnSellerProfileService updates, IValidator<UpdateSellerProfileRequest> updateValidator)
    : ControllerBase
{
    [HttpPost("activation-requests")]
    public async Task<IActionResult> Activate([FromBody] SellerActivationRequest request,
        CancellationToken cancellationToken)
    {
        var token = BearerTokenReader.Read(Request);
        if (token is null)
            return Unauthorized(ApiError.Create(HttpContext, 401, "UNAUTHORIZED", "Sesión no autorizada."));

        var validation = await activationValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return BadRequest(ApiError.Create(HttpContext, 400, "VALIDATION_ERROR", "Solicitud inválida."));

        var result = await activation.ActivateAsync(token, request, cancellationToken);
        return result.Outcome switch
        {
            SellerActivationOutcome.Created => StatusCode(201, result.Response),
            SellerActivationOutcome.AlreadyActive => Ok(result.Response),
            SellerActivationOutcome.Unauthorized => Unauthorized(ApiError.Create(HttpContext, 401,
                "UNAUTHORIZED", "Sesión no autorizada.")),
            SellerActivationOutcome.ProfileMissing => NotFound(ApiError.Create(HttpContext, 404,
                "PROFILE_NOT_FOUND", "Perfil no disponible.")),
            SellerActivationOutcome.AccountNotActive => StatusCode(403, ApiError.Create(HttpContext, 403,
                "SELLER_ACCOUNT_NOT_ACTIVE", "Cuenta no activa.")),
            SellerActivationOutcome.Conflict => Conflict(ApiError.Create(HttpContext, 409,
                "SELLER_STATE_CONFLICT", "El perfil de vendedor no puede activarse en este estado.")),
            SellerActivationOutcome.RateLimited => StatusCode(429,
                ApiError.Create(HttpContext, 429, "RATE_LIMITED", "Intenta de nuevo más tarde.")),
            _ => StatusCode(503,
                ApiError.Create(HttpContext, 503, "SELLER_UNAVAILABLE", "Servicio temporalmente no disponible."))
        };
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var token = BearerTokenReader.Read(Request);
        if (token is null)
            return Unauthorized(ApiError.Create(HttpContext, 401, "UNAUTHORIZED", "Sesión no autorizada."));

        var result = await ownProfile.GetAsync(token, cancellationToken);
        return result.Outcome switch
        {
            OwnSellerProfileOutcome.Found => Ok(result.Response),
            OwnSellerProfileOutcome.Unauthorized => Unauthorized(ApiError.Create(HttpContext, 401,
                "UNAUTHORIZED", "Sesión no autorizada.")),
            OwnSellerProfileOutcome.NotSeller => NotFound(ApiError.Create(HttpContext, 404,
                "SELLER_PROFILE_NOT_FOUND", "Perfil de vendedor no disponible.")),
            OwnSellerProfileOutcome.RateLimited => StatusCode(429,
                ApiError.Create(HttpContext, 429, "RATE_LIMITED", "Intenta de nuevo más tarde.")),
            _ => StatusCode(503,
                ApiError.Create(HttpContext, 503, "SELLER_UNAVAILABLE", "Servicio temporalmente no disponible."))
        };
    }

    [HttpPatch("me")]
    public async Task<IActionResult> UpdateMe([FromBody] JsonElement json, CancellationToken cancellationToken)
    {
        var token = BearerTokenReader.Read(Request);
        if (token is null)
            return Unauthorized(ApiError.Create(HttpContext, 401, "UNAUTHORIZED", "Sesión no autorizada."));

        if (!UpdateSellerProfileRequest.TryParse(json, out var request) || request is null ||
            !(await updateValidator.ValidateAsync(request, cancellationToken)).IsValid)
            return BadRequest(ApiError.Create(HttpContext, 400, "VALIDATION_ERROR", "Solicitud inválida."));

        var result = await updates.UpdateAsync(token, request, cancellationToken);
        return result.Outcome switch
        {
            UpdateOwnSellerProfileOutcome.Updated => Ok(result.Response),
            UpdateOwnSellerProfileOutcome.Unauthorized => Unauthorized(ApiError.Create(HttpContext, 401,
                "UNAUTHORIZED", "Sesión no autorizada.")),
            UpdateOwnSellerProfileOutcome.NotSeller => NotFound(ApiError.Create(HttpContext, 404,
                "SELLER_PROFILE_NOT_FOUND", "Perfil de vendedor no disponible.")),
            UpdateOwnSellerProfileOutcome.Conflict => Conflict(ApiError.Create(HttpContext, 409,
                "SELLER_CONFLICT", "El perfil cambió; vuelve a intentarlo.")),
            UpdateOwnSellerProfileOutcome.RateLimited => StatusCode(429,
                ApiError.Create(HttpContext, 429, "RATE_LIMITED", "Intenta de nuevo más tarde.")),
            _ => StatusCode(503,
                ApiError.Create(HttpContext, 503, "SELLER_UNAVAILABLE", "Servicio temporalmente no disponible."))
        };
    }
}
