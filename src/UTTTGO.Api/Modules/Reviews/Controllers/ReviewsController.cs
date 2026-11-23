using System.Globalization;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using UTTTGO.Api.Modules.Reviews.Application;
using UTTTGO.Api.Modules.Reviews.Contracts;
using UTTTGO.Api.Shared.Contracts;

namespace UTTTGO.Api.Modules.Reviews.Controllers;

[ApiController]
public sealed class ReviewsController(ICreateReviewService create,
    IListSellerReviewsService list, IValidator<CreateReviewRequest> createValidator,
    IValidator<SellerReviewsQuery> listValidator) : ControllerBase
{
    [HttpPost("api/v1/orders/{orderId}/review")]
    [ProducesResponseType(typeof(ReviewResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Create(string orderId, [FromBody] CreateReviewRequest request,
        CancellationToken cancellationToken)
    {
        var token = BearerTokenReader.Read(Request);
        if (token is null)
            return Unauthorized(ApiError.Create(HttpContext, 401, "UNAUTHORIZED", "Sesión no autorizada."));
        if (!(await createValidator.ValidateAsync(request, cancellationToken)).IsValid)
            return BadRequest(ApiError.Create(HttpContext, 400, "VALIDATION_ERROR", "Solicitud inválida."));

        var result = await create.CreateAsync(token, orderId, request, cancellationToken);
        return result.Outcome switch
        {
            CreateReviewOutcome.Created => StatusCode(201, result.Response),
            CreateReviewOutcome.Unauthorized => Unauthorized(ApiError.Create(HttpContext, 401,
                "UNAUTHORIZED", "Sesión no autorizada.")),
            CreateReviewOutcome.AccountNotActive => StatusCode(403, ApiError.Create(HttpContext, 403,
                "ACCOUNT_NOT_ACTIVE", "Cuenta no activa.")),
            CreateReviewOutcome.NotFound => NotFound(ApiError.Create(HttpContext, 404,
                "ORDER_NOT_FOUND", "Pedido no disponible para reseña.")),
            CreateReviewOutcome.AlreadyExists => Conflict(ApiError.Create(HttpContext, 409,
                "REVIEW_ALREADY_EXISTS", "El pedido ya tiene una reseña.")),
            CreateReviewOutcome.RateLimited => StatusCode(429, ApiError.Create(HttpContext, 429,
                "RATE_LIMITED", "Intenta de nuevo más tarde.")),
            _ => StatusCode(503, ApiError.Create(HttpContext, 503,
                "REVIEW_UNAVAILABLE", "Servicio temporalmente no disponible."))
        };
    }

    [HttpGet("api/v1/sellers/{sellerId}/reviews")]
    [ProducesResponseType(typeof(SellerReviewsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> List(string sellerId, [FromQuery] string? page,
        [FromQuery] string? pageSize, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(sellerId, out _))
            return BadRequest(ApiError.Create(HttpContext, 400, "VALIDATION_ERROR", "Solicitud inválida."));
        var query = new SellerReviewsQuery(ParsePage(page), ParsePageSize(pageSize));
        if (!(await listValidator.ValidateAsync(query, cancellationToken)).IsValid)
            return BadRequest(ApiError.Create(HttpContext, 400, "VALIDATION_ERROR", "Solicitud inválida."));

        var result = await list.ListAsync(sellerId, query, cancellationToken);
        return result.Outcome switch
        {
            ListSellerReviewsOutcome.Success => Ok(result.Response),
            ListSellerReviewsOutcome.RateLimited => StatusCode(429, ApiError.Create(HttpContext, 429,
                "RATE_LIMITED", "Intenta de nuevo más tarde.")),
            _ => StatusCode(503, ApiError.Create(HttpContext, 503,
                "REVIEW_UNAVAILABLE", "Servicio temporalmente no disponible."))
        };
    }

    private static int ParsePage(string? value) => Parse(value, SellerReviewsQuery.DefaultPage);

    private static int ParsePageSize(string? value) => Parse(value, SellerReviewsQuery.DefaultPageSize);

    private static int Parse(string? value, int defaultValue) =>
        string.IsNullOrWhiteSpace(value) ? defaultValue :
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
}
