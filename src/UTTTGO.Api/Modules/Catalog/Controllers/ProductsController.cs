using System.Globalization;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using UTTTGO.Api.Modules.Catalog.Application;
using UTTTGO.Api.Modules.Catalog.Contracts;
using UTTTGO.Api.Modules.Catalog.Validators;
using UTTTGO.Api.Shared.Contracts;

namespace UTTTGO.Api.Modules.Catalog.Controllers;

[ApiController]
[Route("api/v1/products")]
public sealed class ProductsController(IListProductsService list, IGetProductService get,
    ICreateProductService create, IUpdateProductService update,
    IUpdateProductAvailabilityService availability, IValidator<ProductListQuery> queryValidator,
    IValidator<CreateProductRequest> createValidator, IValidator<UpdateProductRequest> updateValidator,
    IValidator<UpdateProductAvailabilityRequest> availabilityValidator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ProductListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] string? categoryId,
        [FromQuery] string? minPrice, [FromQuery] string? maxPrice, [FromQuery] string? page,
        [FromQuery] string? pageSize, [FromQuery] string? sortBy, CancellationToken cancellationToken)
    {
        var query = new ProductListQuery(search?.Trim(), categoryId?.Trim(),
            ParsePrice(minPrice), ParsePrice(maxPrice),
            ParsePage(page), ParsePageSize(pageSize),
            sortBy?.Trim() is { Length: > 0 } sort ? sort : ProductSort.CreatedAtDesc);

        if (!(await queryValidator.ValidateAsync(query, cancellationToken)).IsValid)
            return BadRequest(ApiError.Create(HttpContext, 400, "VALIDATION_ERROR", "Solicitud inválida."));

        var result = await list.ListAsync(query, cancellationToken);
        return result.Outcome switch
        {
            ProductListOutcome.Success => Ok(result.Response),
            ProductListOutcome.RateLimited => StatusCode(429,
                ApiError.Create(HttpContext, 429, "RATE_LIMITED", "Intenta de nuevo más tarde.")),
            _ => StatusCode(503,
                ApiError.Create(HttpContext, 503, "CATALOG_UNAVAILABLE", "Servicio temporalmente no disponible."))
        };
    }

    [HttpGet("{productId}")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Detail(string productId, CancellationToken cancellationToken)
    {
        if (!IsProductId(productId))
            return BadRequest(ApiError.Create(HttpContext, 400, "INVALID_PRODUCT_ID", "Identificador inválido."));

        var result = await get.GetAsync(productId, cancellationToken);
        return result.Outcome switch
        {
            GetProductOutcome.Found => Ok(result.Response),
            GetProductOutcome.NotFound => NotFound(ApiError.Create(HttpContext, 404,
                "PRODUCT_NOT_FOUND", "Producto no disponible.")),
            GetProductOutcome.RateLimited => StatusCode(429,
                ApiError.Create(HttpContext, 429, "RATE_LIMITED", "Intenta de nuevo más tarde.")),
            _ => StatusCode(503,
                ApiError.Create(HttpContext, 503, "CATALOG_UNAVAILABLE", "Servicio temporalmente no disponible."))
        };
    }

    [HttpPost]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var token = BearerTokenReader.Read(Request);
        if (token is null)
            return Unauthorized(ApiError.Create(HttpContext, 401, "UNAUTHORIZED", "Sesión no autorizada."));

        if (!(await createValidator.ValidateAsync(request, cancellationToken)).IsValid)
            return BadRequest(ApiError.Create(HttpContext, 400, "VALIDATION_ERROR", "Solicitud inválida."));

        var result = await create.CreateAsync(token, request, cancellationToken);
        return result.Outcome switch
        {
            CreateProductOutcome.Created => StatusCode(201, result.Response),
            CreateProductOutcome.Unauthorized => Unauthorized(ApiError.Create(HttpContext, 401,
                "UNAUTHORIZED", "Sesión no autorizada.")),
            CreateProductOutcome.AccountNotActive => StatusCode(403, ApiError.Create(HttpContext, 403,
                "SELLER_ACCOUNT_NOT_ACTIVE", "Cuenta no activa.")),
            CreateProductOutcome.NotSeller => StatusCode(403, ApiError.Create(HttpContext, 403,
                "SELLER_ROLE_REQUIRED", "Se requiere rol de vendedor.")),
            CreateProductOutcome.CategoryNotFound => NotFound(ApiError.Create(HttpContext, 404,
                "CATEGORY_NOT_FOUND", "Categoría no disponible.")),
            CreateProductOutcome.ImageNotAvailable => StatusCode(422, ApiError.Create(HttpContext, 422,
                "IMAGE_NOT_AVAILABLE", "Las imágenes aún no están disponibles; publica el producto sin foto.")),
            CreateProductOutcome.ImageNotApproved => StatusCode(422, ApiError.Create(HttpContext, 422,
                "IMAGE_NOT_APPROVED", "Imagen no disponible.")),
            CreateProductOutcome.RateLimited => StatusCode(429,
                ApiError.Create(HttpContext, 429, "RATE_LIMITED", "Intenta de nuevo más tarde.")),
            _ => StatusCode(503,
                ApiError.Create(HttpContext, 503, "CATALOG_UNAVAILABLE", "Servicio temporalmente no disponible."))
        };
    }

    [HttpPatch("{productId}")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Update(string productId, [FromBody] System.Text.Json.JsonElement json,
        CancellationToken cancellationToken)
    {
        var token = BearerTokenReader.Read(Request);
        if (token is null)
            return Unauthorized(ApiError.Create(HttpContext, 401, "UNAUTHORIZED", "Sesión no autorizada."));

        if (!IsProductId(productId))
            return BadRequest(ApiError.Create(HttpContext, 400, "INVALID_PRODUCT_ID", "Identificador inválido."));

        if (!UpdateProductRequest.TryParse(json, out var request) || request is null ||
            !(await updateValidator.ValidateAsync(request, cancellationToken)).IsValid)
            return BadRequest(ApiError.Create(HttpContext, 400, "VALIDATION_ERROR", "Solicitud inválida."));

        var result = await update.UpdateAsync(token, productId, request, cancellationToken);
        return result.Outcome switch
        {
            UpdateProductOutcome.Updated => Ok(result.Response),
            UpdateProductOutcome.Unauthorized => Unauthorized(ApiError.Create(HttpContext, 401,
                "UNAUTHORIZED", "Sesión no autorizada.")),
            UpdateProductOutcome.AccountNotActive => StatusCode(403, ApiError.Create(HttpContext, 403,
                "SELLER_ACCOUNT_NOT_ACTIVE", "Cuenta no activa.")),
            UpdateProductOutcome.NotSeller => StatusCode(403, ApiError.Create(HttpContext, 403,
                "SELLER_ROLE_REQUIRED", "Se requiere rol de vendedor.")),
            UpdateProductOutcome.NotFound => NotFound(ApiError.Create(HttpContext, 404,
                "PRODUCT_NOT_FOUND", "Producto no disponible.")),
            UpdateProductOutcome.Conflict => Conflict(ApiError.Create(HttpContext, 409,
                "PRODUCT_CONFLICT", "El producto cambió; vuelve a intentarlo.")),
            UpdateProductOutcome.ImageNotApproved => StatusCode(422, ApiError.Create(HttpContext, 422,
                "IMAGE_NOT_APPROVED", "Imagen no disponible.")),
            UpdateProductOutcome.RateLimited => StatusCode(429,
                ApiError.Create(HttpContext, 429, "RATE_LIMITED", "Intenta de nuevo más tarde.")),
            _ => StatusCode(503,
                ApiError.Create(HttpContext, 503, "CATALOG_UNAVAILABLE", "Servicio temporalmente no disponible."))
        };
    }

    [HttpPatch("{productId}/availability")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> UpdateAvailability(string productId,
        [FromBody] UpdateProductAvailabilityRequest request, CancellationToken cancellationToken)
    {
        var token = BearerTokenReader.Read(Request);
        if (token is null)
            return Unauthorized(ApiError.Create(HttpContext, 401, "UNAUTHORIZED", "Sesión no autorizada."));

        if (!IsProductId(productId))
            return BadRequest(ApiError.Create(HttpContext, 400, "INVALID_PRODUCT_ID", "Identificador inválido."));

        if (!(await availabilityValidator.ValidateAsync(request, cancellationToken)).IsValid)
            return BadRequest(ApiError.Create(HttpContext, 400, "VALIDATION_ERROR", "Solicitud inválida."));

        var result = await availability.UpdateAsync(token, productId, request, cancellationToken);
        return result.Outcome switch
        {
            UpdateProductAvailabilityOutcome.Updated => Ok(result.Response),
            UpdateProductAvailabilityOutcome.Unauthorized => Unauthorized(ApiError.Create(HttpContext, 401,
                "UNAUTHORIZED", "Sesión no autorizada.")),
            UpdateProductAvailabilityOutcome.AccountNotActive => StatusCode(403, ApiError.Create(HttpContext, 403,
                "SELLER_ACCOUNT_NOT_ACTIVE", "Cuenta no activa.")),
            UpdateProductAvailabilityOutcome.NotSeller => StatusCode(403, ApiError.Create(HttpContext, 403,
                "SELLER_ROLE_REQUIRED", "Se requiere rol de vendedor.")),
            UpdateProductAvailabilityOutcome.NotFound => NotFound(ApiError.Create(HttpContext, 404,
                "PRODUCT_NOT_FOUND", "Producto no disponible.")),
            UpdateProductAvailabilityOutcome.Conflict => Conflict(ApiError.Create(HttpContext, 409,
                "PRODUCT_CONFLICT", "El producto cambió; vuelve a intentarlo.")),
            UpdateProductAvailabilityOutcome.RateLimited => StatusCode(429,
                ApiError.Create(HttpContext, 429, "RATE_LIMITED", "Intenta de nuevo más tarde.")),
            _ => StatusCode(503,
                ApiError.Create(HttpContext, 503, "CATALOG_UNAVAILABLE", "Servicio temporalmente no disponible."))
        };
    }

    private static bool IsProductId(string? productId) =>
        productId is { Length: 24 } && productId.All(Uri.IsHexDigit);

    private static decimal? ParsePrice(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : decimal.MinValue;
    }

    private static int ParsePage(string? value)
    {
        // An absent parameter takes the default; a present but invalid one is rejected by the validator.
        if (string.IsNullOrWhiteSpace(value)) return 1;
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private static int ParsePageSize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return ProductListQuery.DefaultPageSize;
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }
}
