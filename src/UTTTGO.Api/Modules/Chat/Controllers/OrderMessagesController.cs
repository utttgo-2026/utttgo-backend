using Microsoft.AspNetCore.Mvc;
using UTTTGO.Api.Modules.Chat.Application;
using UTTTGO.Api.Shared.Contracts;

namespace UTTTGO.Api.Modules.Chat.Controllers;

[ApiController, Route("api/v1/orders/{orderId}/messages")]
public sealed class OrderMessagesController(IChatService chat) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(OrderMessagesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(string orderId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (page < 1 || pageSize is < 1 or > 50) return BadRequest(ApiError.Create(HttpContext, 400, "VALIDATION_ERROR", "Solicitud inválida."));
        var token = BearerTokenReader.Read(Request); if (token is null) return Unauthorized(ApiError.Create(HttpContext, 401, "UNAUTHORIZED", "Sesión no autorizada."));
        return Map(await chat.ListAsync(token, orderId, page, pageSize, ct));
    }
    [HttpPost]
    [ProducesResponseType(typeof(OrderMessageResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Post(string orderId, [FromBody] SendOrderMessageRequest request, [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken ct)
    {
        var token = BearerTokenReader.Read(Request); if (token is null) return Unauthorized(ApiError.Create(HttpContext, 401, "UNAUTHORIZED", "Sesión no autorizada."));
        if (string.IsNullOrWhiteSpace(key)) return BadRequest(ApiError.Create(HttpContext, 400, "IDEMPOTENCY_KEY_REQUIRED", "Idempotency-Key es obligatorio."));
        if (request is null) return BadRequest(ApiError.Create(HttpContext, 400, "VALIDATION_ERROR", "Solicitud inválida."));
        return Map(await chat.SendAsync(token, orderId, request.Content, key, ct));
    }
    private IActionResult Map(ChatResult result) => result.Outcome switch
    {
        ChatOutcome.Success => Ok(result.Page),
        ChatOutcome.Created => StatusCode(201, result.Message),
        ChatOutcome.Duplicate => StatusCode(201, result.Message),
        ChatOutcome.Unauthorized => Unauthorized(ApiError.Create(HttpContext, 401, "UNAUTHORIZED", "Sesión no autorizada.")),
        ChatOutcome.NotFound => NotFound(ApiError.Create(HttpContext, 404, "ORDER_NOT_FOUND", "Pedido no disponible.")),
        ChatOutcome.Closed => Conflict(ApiError.Create(HttpContext, 409, "ORDER_CHAT_CLOSED", "El chat está cerrado.")),
        ChatOutcome.IdempotencyKeyReused => Conflict(ApiError.Create(HttpContext, 409, "IDEMPOTENCY_KEY_REUSED", "La Idempotency-Key ya fue utilizada con otro contenido.")),
        ChatOutcome.InvalidContent => BadRequest(ApiError.Create(HttpContext, 400, "VALIDATION_ERROR", "Solicitud inválida.")),
        ChatOutcome.InvalidIdempotencyKey => BadRequest(ApiError.Create(HttpContext, 400, "VALIDATION_ERROR", "Solicitud inválida.")),
        ChatOutcome.RateLimited => StatusCode(429, ApiError.Create(HttpContext, 429, "RATE_LIMITED", "Intenta de nuevo más tarde.")),
        _ => StatusCode(503, ApiError.Create(HttpContext, 503, "CHAT_UNAVAILABLE", "Servicio temporalmente no disponible."))
    };
}
