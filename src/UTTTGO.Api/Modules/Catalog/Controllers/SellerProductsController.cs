using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using UTTTGO.Api.Modules.Catalog.Application;
using UTTTGO.Api.Modules.Catalog.Contracts;
using UTTTGO.Api.Shared.Contracts;

namespace UTTTGO.Api.Modules.Catalog.Controllers;

[ApiController]
[Route("api/v1/sellers/me/products")]
public sealed class SellerProductsController(IListOwnProductsService list,
    IValidator<ProductListQuery> queryValidator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ProductListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> List([FromQuery] string? page, [FromQuery] string? pageSize,
        CancellationToken cancellationToken)
    {
        var token = BearerTokenReader.Read(Request);
        if (token is null)
            return Unauthorized(ApiError.Create(HttpContext, 401, "UNAUTHORIZED", "Sesión no autorizada."));

        var query = new ProductListQuery(null, null, null, null, ParsePage(page), ParsePageSize(pageSize),
            ProductSort.CreatedAtDesc);
        if (!(await queryValidator.ValidateAsync(query, cancellationToken)).IsValid)
            return BadRequest(ApiError.Create(HttpContext, 400, "VALIDATION_ERROR", "Solicitud inválida."));

        var result = await list.ListAsync(token, query.Page, query.PageSize, cancellationToken);
        return result.Outcome switch
        {
            ListOwnProductsOutcome.Success => Ok(result.Response),
            ListOwnProductsOutcome.Unauthorized => Unauthorized(ApiError.Create(HttpContext, 401,
                "UNAUTHORIZED", "Sesión no autorizada.")),
            ListOwnProductsOutcome.AccountNotActive => StatusCode(403, ApiError.Create(HttpContext, 403,
                "SELLER_ACCOUNT_NOT_ACTIVE", "Cuenta no activa.")),
            ListOwnProductsOutcome.NotSeller => StatusCode(403, ApiError.Create(HttpContext, 403,
                "SELLER_ROLE_REQUIRED", "Se requiere rol de vendedor.")),
            ListOwnProductsOutcome.RateLimited => StatusCode(429,
                ApiError.Create(HttpContext, 429, "RATE_LIMITED", "Intenta de nuevo más tarde.")),
            _ => StatusCode(503,
                ApiError.Create(HttpContext, 503, "CATALOG_UNAVAILABLE", "Servicio temporalmente no disponible."))
        };
    }

    private static int ParsePage(string? value) =>
        string.IsNullOrWhiteSpace(value) ? 1 : int.TryParse(value, out var page) ? page : 0;

    private static int ParsePageSize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? ProductListQuery.DefaultPageSize :
        int.TryParse(value, out var pageSize) ? pageSize : 0;
}
