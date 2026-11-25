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
    IGetOrderService get, ICancelOrderService cancel, IUpdateOrderStatusService updateStatus,
    ICreateDeliveryTokenService deliveryToken, IConfirmDeliveryService confirmDelivery,
    IValidator<CreateOrderRequest> createValidator, IValidator<OrderListQuery> listValidator,
    IValidator<UpdateOrderStatusRequest> statusValidator,
    IValidator<DeliveryConfirmationRequest> deliveryValidator) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var token = BearerTokenReader.Read(Request);
        if (token is null)
            return Unauthorized(ApiError.Create(HttpContext, 401, "UNAUTHORIZED", "Sesión no autorizada."));

        if (!(await createValidator.ValidateAsync(request, cancellationToken)).IsValid)
            return BadRequest(ApiError.Create(HttpContext, 400, "VALIDATION_ERROR", "Solicitud inválida."));

        var result = await create.CreateAsync(token, request, idempotencyKey, HttpContext.TraceIdentifier,
            cancellationToken);
        return result.Outcome switch
        {
            CreateOrderOutcome.Created => StatusCode(201, result.Response),
            CreateOrderOutcome.AlreadyCreated => Ok(result.Response),
            CreateOrderOutcome.IdempotencyKeyReused => Conflict(ApiError.Create(HttpContext, 409,
                "IDEMPOTENCY_KEY_REUSED", "La Idempotency-Key ya fue utilizada con otro cuerpo.")),
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
    [ProducesResponseType(typeof(OrderListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status503ServiceUnavailable)]
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
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status503ServiceUnavailable)]
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

    [HttpPost("{orderId}/cancel")]
    [ProducesResponseType(typeof(OrderCancellationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Cancel(string orderId, CancellationToken cancellationToken)
    {
        var token = BearerTokenReader.Read(Request);
        if (token is null)
            return Unauthorized(ApiError.Create(HttpContext, 401, "UNAUTHORIZED", "Sesión no autorizada."));

        var result = await cancel.CancelAsync(token, orderId, HttpContext.TraceIdentifier,
            cancellationToken);
        return result.Outcome switch
        {
            CancelOrderOutcome.Cancelled or CancelOrderOutcome.AlreadyCancelled => Ok(result.Response),
            CancelOrderOutcome.Unauthorized => Unauthorized(ApiError.Create(HttpContext, 401,
                "UNAUTHORIZED", "Sesión no autorizada.")),
            CancelOrderOutcome.AccountNotActive => StatusCode(403, ApiError.Create(HttpContext, 403,
                "ACCOUNT_NOT_ACTIVE", "Cuenta no activa.")),
            CancelOrderOutcome.Forbidden => StatusCode(403, ApiError.Create(HttpContext, 403,
                "ORDER_FORBIDDEN", "El pedido no pertenece al comprador.")),
            CancelOrderOutcome.NotFound => NotFound(ApiError.Create(HttpContext, 404,
                "ORDER_NOT_FOUND", "Pedido no disponible.")),
            CancelOrderOutcome.StateConflict => StatusCode(409, ApiError.Create(HttpContext, 409,
                "ORDER_STATE_CONFLICT", "El pedido no se puede cancelar en su estado actual.")),
            CancelOrderOutcome.RateLimited => StatusCode(429,
                ApiError.Create(HttpContext, 429, "RATE_LIMITED", "Intenta de nuevo más tarde.")),
            _ => StatusCode(503,
                ApiError.Create(HttpContext, 503, "ORDER_UNAVAILABLE", "Servicio temporalmente no disponible."))
        };
    }

    [HttpPatch("{orderId}/status")]
    [ProducesResponseType(typeof(OrderStatusUpdateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> UpdateStatus(string orderId, [FromBody] UpdateOrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        var token = BearerTokenReader.Read(Request);
        if (token is null)
            return Unauthorized(ApiError.Create(HttpContext, 401, "UNAUTHORIZED", "Sesión no autorizada."));

        if (!(await statusValidator.ValidateAsync(request, cancellationToken)).IsValid)
            return BadRequest(ApiError.Create(HttpContext, 400, "VALIDATION_ERROR", "Solicitud inválida."));

        var result = await updateStatus.UpdateAsync(token, orderId, request, HttpContext.TraceIdentifier,
            cancellationToken);
        return result.Outcome switch
        {
            UpdateOrderStatusOutcome.Updated => Ok(result.Response),
            UpdateOrderStatusOutcome.Unauthorized => Unauthorized(ApiError.Create(HttpContext, 401,
                "UNAUTHORIZED", "Sesión no autorizada.")),
            UpdateOrderStatusOutcome.AccountNotActive => StatusCode(403, ApiError.Create(HttpContext, 403,
                "ACCOUNT_NOT_ACTIVE", "Cuenta no activa.")),
            UpdateOrderStatusOutcome.Forbidden => StatusCode(403, ApiError.Create(HttpContext, 403,
                "ORDER_FORBIDDEN", "El pedido no pertenece al vendedor.")),
            UpdateOrderStatusOutcome.NotFound => NotFound(ApiError.Create(HttpContext, 404,
                "ORDER_NOT_FOUND", "Pedido no disponible.")),
            UpdateOrderStatusOutcome.StateConflict => StatusCode(409, ApiError.Create(HttpContext, 409,
                "ORDER_STATE_CONFLICT", "Transición de estado no permitida.")),
            UpdateOrderStatusOutcome.VersionConflict => StatusCode(409, ApiError.Create(HttpContext, 409,
                "ORDER_VERSION_CONFLICT", "El pedido cambió; vuelve a leerlo.")),
            UpdateOrderStatusOutcome.RateLimited => StatusCode(429,
                ApiError.Create(HttpContext, 429, "RATE_LIMITED", "Intenta de nuevo más tarde.")),
            _ => StatusCode(503,
                ApiError.Create(HttpContext, 503, "ORDER_UNAVAILABLE", "Servicio temporalmente no disponible."))
        };
    }

    [HttpPost("{orderId}/delivery-token")]
    [ProducesResponseType(typeof(DeliveryTokenResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> DeliveryToken(string orderId, CancellationToken cancellationToken)
    {
        var token = BearerTokenReader.Read(Request);
        if (token is null)
            return Unauthorized(ApiError.Create(HttpContext, 401, "UNAUTHORIZED", "Sesión no autorizada."));

        var result = await deliveryToken.CreateAsync(token, orderId, HttpContext.TraceIdentifier,
            cancellationToken);
        return result.Outcome switch
        {
            CreateDeliveryTokenOutcome.Created => StatusCode(201, result.Response),
            CreateDeliveryTokenOutcome.Unauthorized => Unauthorized(ApiError.Create(HttpContext, 401,
                "UNAUTHORIZED", "Sesión no autorizada.")),
            CreateDeliveryTokenOutcome.AccountNotActive => StatusCode(403, ApiError.Create(HttpContext, 403,
                "ACCOUNT_NOT_ACTIVE", "Cuenta no activa.")),
            CreateDeliveryTokenOutcome.Forbidden => StatusCode(403, ApiError.Create(HttpContext, 403,
                "ORDER_FORBIDDEN", "El pedido no pertenece al comprador.")),
            CreateDeliveryTokenOutcome.NotFound => NotFound(ApiError.Create(HttpContext, 404,
                "ORDER_NOT_FOUND", "Pedido no disponible.")),
            CreateDeliveryTokenOutcome.StateConflict => StatusCode(409, ApiError.Create(HttpContext, 409,
                "ORDER_STATE_CONFLICT", "El token solo se genera cuando el pedido está listo.")),
            CreateDeliveryTokenOutcome.RateLimited => StatusCode(429,
                ApiError.Create(HttpContext, 429, "RATE_LIMITED", "Intenta de nuevo más tarde.")),
            _ => StatusCode(503,
                ApiError.Create(HttpContext, 503, "ORDER_UNAVAILABLE", "Servicio temporalmente no disponible."))
        };
    }

    [HttpPost("{orderId}/delivery-confirmations")]
    [ProducesResponseType(typeof(DeliveryConfirmationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ConfirmDelivery(string orderId,
        [FromBody] DeliveryConfirmationRequest request, CancellationToken cancellationToken)
    {
        var token = BearerTokenReader.Read(Request);
        if (token is null)
            return Unauthorized(ApiError.Create(HttpContext, 401, "UNAUTHORIZED", "Sesión no autorizada."));

        if (!(await deliveryValidator.ValidateAsync(request, cancellationToken)).IsValid)
            return BadRequest(ApiError.Create(HttpContext, 400, "VALIDATION_ERROR", "Solicitud inválida."));

        var result = await confirmDelivery.ConfirmAsync(token, orderId, request, HttpContext.TraceIdentifier,
            cancellationToken);
        return result.Outcome switch
        {
            ConfirmDeliveryOutcome.Delivered => Ok(result.Response),
            ConfirmDeliveryOutcome.Unauthorized => Unauthorized(ApiError.Create(HttpContext, 401,
                "UNAUTHORIZED", "Sesión no autorizada.")),
            ConfirmDeliveryOutcome.AccountNotActive => StatusCode(403, ApiError.Create(HttpContext, 403,
                "ACCOUNT_NOT_ACTIVE", "Cuenta no activa.")),
            ConfirmDeliveryOutcome.Forbidden => StatusCode(403, ApiError.Create(HttpContext, 403,
                "ORDER_FORBIDDEN", "El pedido no pertenece al vendedor.")),
            ConfirmDeliveryOutcome.NotFound => NotFound(ApiError.Create(HttpContext, 404,
                "ORDER_NOT_FOUND", "Pedido no disponible.")),
            ConfirmDeliveryOutcome.StateConflict => StatusCode(409, ApiError.Create(HttpContext, 409,
                "ORDER_STATE_CONFLICT", "El pedido no está listo para entregarse.")),
            ConfirmDeliveryOutcome.TokenAlreadyUsed => StatusCode(409, ApiError.Create(HttpContext, 409,
                "DELIVERY_TOKEN_ALREADY_USED", "El token de entrega ya fue utilizado.")),
            ConfirmDeliveryOutcome.TokenInvalid => StatusCode(422, ApiError.Create(HttpContext, 422,
                "DELIVERY_TOKEN_INVALID", "Token de entrega inválido.")),
            ConfirmDeliveryOutcome.TokenExpired => StatusCode(422, ApiError.Create(HttpContext, 422,
                "DELIVERY_TOKEN_EXPIRED", "El token de entrega expiró.")),
            ConfirmDeliveryOutcome.RateLimited => StatusCode(429,
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
