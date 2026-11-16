using System.Globalization;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using UTTTGO.Api.Modules.Orders.Application;
using UTTTGO.Api.Modules.Orders.Contracts;
using UTTTGO.Api.Modules.Orders.Validators;
using UTTTGO.Api.Shared.Contracts;

namespace UTTTGO.Api.Modules.Orders.Controllers;

[ApiController]
[Route("api/v1/orders")]
public sealed class OrdersController(ICreateOrderService create, IListOwnOrdersService list,
    IGetOrderService get, IValidator<CreateOrderRequest> createValidator,
    IValidator<OrderListQuery> listValidator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var token = BearerTokenReader.Read(Request);
        if (token is null)
            return Unauthorized(ApiError.Create(HttpContext, 401, "UNAUTHORIZED", "Sesión no autorizada."));

        if (!(await createValidator.ValidateAsync(request, cancellationToken)).IsValid)
            return BadRequest(ApiError.Create(HttpContext, 400, "VALIDATION_ERROR", "Solicitud inválida."));

        var idempotencyKey = Request.Headers["Idempotency-Key"].ToString();
        var result = await create.CreateAsync(token, request, idempotencyKey, HttpContext.TraceIdentifier,
            cancellationToken);
        return result.Outcome switch
        {
            CreateOrderOutcome.Created => StatusCode(201, result.Response),
            CreateOrderOutcome.AlreadyCreated => Ok(result.Response),
            CreateOrderOutcome.Unauthorized => Unauthorized(ApiError.Create(HttpContext, 401,
                "UNAUTHORIZED", "Sesión no autorizada.")),
            CreateOrderOutcome.AccountNotActive => StatusCode(403, ApiError.Create(HttpContext, 403,
                "ACCOUNT_NOT_ACTIVE", "Cuenta no activa.")),
            CreateOrderOutcome.ProductNotFound => NotFound(ApiError.Create(HttpContext, 404,
                "PRODUCT_NOT_FOUND", "Producto no disponible.")),
            CreateOrderOutcome.ProductNotAvailable => StatusCode(422, ApiError.Create(HttpContext, 422,
                "PRODUCT_NOT_AVAILABLE", "Producto no disponible para compra.")),
            CreateOrderOutcome.InsufficientStock => BadRequest(ApiError.Create(HttpContext, 400,
                "INSUFFICIENT_STOCK", "No hay existencias suficientes.")),
            CreateOrderOutcome.RateLimited => StatusCode(429,
                ApiError.Create(HttpContext, 429, "RATE_LIMITED", "Intenta de nuevo más tarde.")),
            _ => StatusCode(503,
                ApiError.Create(HttpContext, 503, "ORDER_UNAVAILABLE", "Servicio temporalmente no disponible."))
        };
    }

    [HttpGet("mine")]
    public async Task<IActionResult> Mine([FromQuery] string? page, [FromQuery] string? pageSize,
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
            ListOwnOrdersOutcome.Success => Ok(result.Response),
            ListOwnOrdersOutcome.Unauthorized => Unauthorized(ApiError.Create(HttpContext, 401,
                "UNAUTHORIZED", "Sesión no autorizada.")),
            ListOwnOrdersOutcome.RateLimited => StatusCode(429,
                ApiError.Create(HttpContext, 429, "RATE_LIMITED", "Intenta de nuevo más tarde.")),
            _ => StatusCode(503,
                ApiError.Create(HttpContext, 503, "ORDER_UNAVAILABLE", "Servicio temporalmente no disponible."))
        };
    }

    [HttpGet("{orderId}")]
    public async Task<IActionResult> Detail(string orderId, CancellationToken cancellationToken)
    {
        var token = BearerTokenReader.Read(Request);
        if (token is null)
            return Unauthorized(ApiError.Create(HttpContext, 401, "UNAUTHORIZED", "Sesión no autorizada."));

        var result = await get.GetAsync(token, orderId, cancellationToken);
        return result.Outcome switch
        {
            GetOrderOutcome.Found => Ok(result.Response),
            GetOrderOutcome.Unauthorized => Unauthorized(ApiError.Create(HttpContext, 401,
                "UNAUTHORIZED", "Sesión no autorizada.")),
            GetOrderOutcome.NotFound => NotFound(ApiError.Create(HttpContext, 404,
                "ORDER_NOT_FOUND", "Pedido no disponible.")),
            GetOrderOutcome.RateLimited => StatusCode(429,
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
