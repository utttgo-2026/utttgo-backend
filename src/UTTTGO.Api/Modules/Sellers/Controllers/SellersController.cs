using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using UTTTGO.Api.Modules.Identity.Contracts;
using UTTTGO.Api.Modules.Sellers.Application;
using UTTTGO.Api.Modules.Sellers.Contracts;

namespace UTTTGO.Api.Modules.Sellers.Controllers;

[ApiController]
[Route("api/v1/sellers")]
public sealed class SellersController(ISellerProfileService sellers, IValidator<SellerIdRequest> validator)
    : ControllerBase
{
    [HttpGet("{sellerId}")]
    public async Task<IActionResult> Get([FromRoute] string sellerId, CancellationToken cancellationToken)
    {
        if (!(await validator.ValidateAsync(new SellerIdRequest(sellerId), cancellationToken)).IsValid)
            return BadRequest(ApiError.Create(HttpContext, 400, "INVALID_SELLER_ID", "Identificador inválido."));

        var result = await sellers.GetAsync(sellerId.ToLowerInvariant(), cancellationToken);
        return result.Outcome switch
        {
            SellerProfileOutcome.Found when result.Response is not null => Ok(result.Response),
            SellerProfileOutcome.NotFound => NotFound(ApiError.Create(HttpContext, 404,
                "SELLER_NOT_FOUND", "Vendedor no disponible.")),
            SellerProfileOutcome.RateLimited => StatusCode(429, ApiError.Create(HttpContext, 429,
                "RATE_LIMITED", "Intenta de nuevo más tarde.")),
            _ => StatusCode(503, ApiError.Create(HttpContext, 503,
                "SELLER_UNAVAILABLE", "Servicio temporalmente no disponible."))
        };
    }
}
