using Microsoft.AspNetCore.Mvc;
using UTTTGO.Api.Modules.Media.Application;
using UTTTGO.Api.Modules.Media.Contracts;
using UTTTGO.Api.Shared.Contracts;

namespace UTTTGO.Api.Modules.Media.Controllers;

[ApiController]
[Route("api/v1/media")]
public class MediaController : ControllerBase
{
    private readonly ISignedUrlService _signedUrlService;
    private readonly IMediaAssetService _mediaAssetService;

    public MediaController(ISignedUrlService signedUrlService, IMediaAssetService mediaAssetService)
    {
        _signedUrlService = signedUrlService;
        _mediaAssetService = mediaAssetService;
    }

    [HttpPost("presigned-url")]
    [ProducesResponseType(typeof(PresignedUrlResponse), 201)]
    [ProducesResponseType(typeof(ApiError), 400)]
    [ProducesResponseType(typeof(ApiError), 429)]
    public async Task<IActionResult> GetPresignedUrl([FromBody] PresignedUrlRequest request, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiError.Create(HttpContext, 400, "VALIDATION_ERROR", "Datos de solicitud inválidos"));
        }

        try
        {
            var response = await _signedUrlService.GeneratePresignedUrlAsync(request, cancellationToken);
            return Created("", response);
        }
        catch (FluentValidation.ValidationException ex)
        {
            return BadRequest(ApiError.Create(HttpContext, 400, "VALIDATION_ERROR", ex.Message));
        }
        catch (Exception)
        {
            return StatusCode(503, ApiError.Create(HttpContext, 503, "UNAVAILABLE", "Servicio temporalmente no disponible"));
        }
    }

    [HttpPost("confirm")]
    [ProducesResponseType(typeof(ConfirmResponse), 200)]
    [ProducesResponseType(typeof(ApiError), 400)]
    [ProducesResponseType(typeof(ApiError), 404)]
    [ProducesResponseType(typeof(ApiError), 429)]
    public async Task<IActionResult> ConfirmAsset([FromBody] ConfirmRequest request, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiError.Create(HttpContext, 400, "VALIDATION_ERROR", "Datos de solicitud inválidos"));
        }

        try
        {
            var response = await _mediaAssetService.ConfirmAssetAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (FluentValidation.ValidationException ex)
        {
            return BadRequest(ApiError.Create(HttpContext, 400, "VALIDATION_ERROR", ex.Message));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiError.Create(HttpContext, 404, "ASSET_NOT_FOUND", "Asset no encontrado"));
        }
        catch (Exception)
        {
            return StatusCode(503, ApiError.Create(HttpContext, 503, "UNAVAILABLE", "Servicio temporalmente no disponible"));
        }
    }
}