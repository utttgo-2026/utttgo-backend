using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Media.Application;
using UTTTGO.Api.Shared.Contracts;

namespace UTTTGO.Api.Modules.Identity.Controllers;

[ApiController]
[Route("api/v1/users")]
public sealed class AccountDeletionController(IDeleteOwnAccountService deletion,
    IMediaDeletionStatusReader mediaDeletion) : ControllerBase
{
    [HttpDelete("me")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(AccountDeletionUnavailableResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> DeleteMe(
        [FromHeader(Name = "X-Account-Deletion-Id"), BindRequired] string operationId,
        [FromHeader(Name = "X-Account-Deletion-Proof"), BindRequired] string proof,
        CancellationToken cancellationToken)
    {
        var token = BearerTokenReader.Read(Request);
        if (token is null)
            return Unauthorized(ApiError.Create(HttpContext, 401, "UNAUTHORIZED", "Sesión no autorizada."));

        var result = await deletion.DeleteAsync(token, operationId, proof, cancellationToken);
        return result.Outcome switch
        {
            DeleteOwnAccountOutcome.Success => NoContent(),
            DeleteOwnAccountOutcome.InvalidRequest => BadRequest(ApiError.Create(HttpContext, 400,
                "ACCOUNT_DELETION_HEADERS_REQUIRED", "Se requieren identificador y prueba de operación.")),
            DeleteOwnAccountOutcome.Unauthorized => Unauthorized(ApiError.Create(HttpContext, 401,
                "UNAUTHORIZED", "Sesión no autorizada.")),
            DeleteOwnAccountOutcome.Conflict => Conflict(ApiError.Create(HttpContext, 409,
                "ACTIVE_ORDER_CONFLICT", "La cuenta tiene pedidos activos.")),
            _ => StatusCode(503, new AccountDeletionUnavailableResponse(
                ApiError.Create(HttpContext, 503, "ACCOUNT_DELETION_UNAVAILABLE",
                    "Servicio temporalmente no disponible."),
                result.Operation?.OperationId, result.Operation?.Status, result.Operation?.Stage))
        };
    }

    [HttpGet("me/deletion/{operationId}")]
    [ProducesResponseType(typeof(AccountDeletionStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDeletion(string operationId,
        [FromHeader(Name = "X-Account-Deletion-Proof"), BindRequired] string proof,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(operationId, out _) || string.IsNullOrWhiteSpace(proof) || proof.Length < 32)
            return BadRequest(ApiError.Create(HttpContext, 400, "ACCOUNT_DELETION_HEADERS_REQUIRED",
                "Se requiere una prueba de operación válida."));

        var store = HttpContext.RequestServices.GetRequiredService<IAccountDeletionOperationStore>();
        var operation = await store.FindAsync(operationId, AccountDeletionProof.Hash(proof), cancellationToken);
        if (operation is null)
            return NotFound(ApiError.Create(HttpContext, 404, "ACCOUNT_DELETION_NOT_FOUND",
                "Operación no encontrada."));

        var media = await mediaDeletion.FindDeletionStatusAsync(operation.UserId,
            operation.DeletedUserRef, cancellationToken);
        return Ok(new AccountDeletionStatusResponse(operation.OperationId, operation.Status, operation.Stage,
            operation.FailureCode, media is null
                ? new("NotQueued", 0, null)
                : new(media.Status, media.Attempts, media.NextAttemptAt)));
    }
}

public sealed record AccountDeletionStatusResponse(string OperationId, string Status, string Stage,
    string? FailureCode, AccountDeletionMediaPurgeStatus MediaPurgeStatus);

public sealed record AccountDeletionMediaPurgeStatus(string Status, int Attempts, DateTime? NextAttemptAt);

public sealed record AccountDeletionUnavailableResponse(ApiError Error, string? OperationId,
    string? State, string? Stage);
