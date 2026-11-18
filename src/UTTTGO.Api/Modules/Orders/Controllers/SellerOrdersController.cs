using System.Globalization;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using UTTTGO.Api.Modules.Orders.Application;
using UTTTGO.Api.Modules.Orders.Contracts;
using UTTTGO.Api.Shared.Contracts;

namespace UTTTGO.Api.Modules.Orders.Controllers;

[ApiController]
[Route("api/v1/sellers/me/orders")]
public sealed class SellerOrdersController(IListSellerOrdersService list,
    IValidator<OrderListQuery> listValidator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(OrderListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Received([FromQuery] string? page, [FromQuery] string? pageSize,
        [FromQuery] string? status, CancellationToken cancellationToken)
    {
        var token = BearerTokenReader.Read(Request);
        if (token is null)
            return Unauthorized(ApiError.Create(HttpContext, 401, "UNAUTHORIZED", "Sesión no autorizada."));

        var query = new OrderListQuery(ParsePage(page), ParsePageSize(pageSize),
            status?.Trim() is { Length: > 0 } value ? value : null);
        if (!(await listValidator.ValidateAsync(query, cancellationToken)).IsValid)
            return BadRequest(ApiError.Create(HttpContext, 400, "VALIDATION_ERROR", "Solicitud inválida."));

        var result = await list.ListAsync(token, query, cancellationToken);
        return result.Outcome switch
        {
            ListSellerOrdersOutcome.Success => Ok(result.Response),
            ListSellerOrdersOutcome.Unauthorized => Unauthorized(ApiError.Create(HttpContext, 401,
                "UNAUTHORIZED", "Sesión no autorizada.")),
            ListSellerOrdersOutcome.AccountNotActive => StatusCode(403, ApiError.Create(HttpContext, 403,
                "SELLER_ACCOUNT_NOT_ACTIVE", "Cuenta no activa.")),
            ListSellerOrdersOutcome.NotSeller => StatusCode(403, ApiError.Create(HttpContext, 403,
                "SELLER_ROLE_REQUIRED", "Se requiere rol de vendedor.")),
            ListSellerOrdersOutcome.RateLimited => StatusCode(429,
                ApiError.Create(HttpContext, 429, "RATE_LIMITED", "Intenta de nuevo más tarde.")),
            _ => StatusCode(503,
                ApiError.Create(HttpContext, 503, "ORDER_UNAVAILABLE", "Servicio temporalmente no disponible."))
        };
    }

    private static int ParsePage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 1;
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private static int ParsePageSize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return OrderListQuery.DefaultPageSize;
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }
}
