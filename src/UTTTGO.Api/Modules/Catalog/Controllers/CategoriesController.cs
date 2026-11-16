using System.Globalization;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using UTTTGO.Api.Modules.Catalog.Application;
using UTTTGO.Api.Modules.Catalog.Contracts;
using UTTTGO.Api.Shared.Contracts;

namespace UTTTGO.Api.Modules.Catalog.Controllers;

[ApiController]
[Route("api/v1/categories")]
public sealed class CategoriesController(IListCategoriesService categories) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(CategoryListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> List([FromQuery] string? page, [FromQuery] string? pageSize,
        CancellationToken cancellationToken)
    {
        var parsedPage = string.IsNullOrWhiteSpace(page)
            ? 1
            : int.TryParse(page, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pageValue)
                ? pageValue
                : 0;
        var parsedPageSize = string.IsNullOrWhiteSpace(pageSize)
            ? ProductListQuery.DefaultPageSize
            : int.TryParse(pageSize, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pageSizeValue)
                ? pageSizeValue
                : 0;
        if (parsedPage < 1 || parsedPage > ProductListQuery.MaxPage || parsedPageSize < 1 ||
            parsedPageSize > ProductListQuery.MaxPageSize)
            return BadRequest(ApiError.Create(HttpContext, 400, "VALIDATION_ERROR", "Solicitud inválida."));

        var result = await categories.ListAsync(parsedPage, parsedPageSize, cancellationToken);
        return result.Outcome switch
        {
            CategoryListOutcome.Success => Ok(result.Response),
            CategoryListOutcome.RateLimited => StatusCode(429,
                ApiError.Create(HttpContext, 429, "RATE_LIMITED", "Intenta de nuevo más tarde.")),
            _ => StatusCode(503,
                ApiError.Create(HttpContext, 503, "CATALOG_UNAVAILABLE", "Servicio temporalmente no disponible."))
        };
    }
}
