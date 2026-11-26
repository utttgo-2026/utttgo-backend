using Microsoft.AspNetCore.Mvc;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Shared.Contracts;

namespace UTTTGO.Api.Modules.Identity.Controllers;

[ApiController]
[Route("api/v1/users")]
public sealed class AccountDeletionController(IDeleteOwnAccountService deletion) : ControllerBase
{
    [HttpDelete("me")]
    public async Task<IActionResult> DeleteMe(CancellationToken cancellationToken)
    {
        var token = BearerTokenReader.Read(Request);
        if (token is null)
            return Unauthorized(ApiError.Create(HttpContext, 401, "UNAUTHORIZED", "Sesión no autorizada."));

        var operationId = Request.Headers["X-Account-Deletion-Id"].ToString();
        var proof = Request.Headers["X-Account-Deletion-Proof"].ToString();
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
    public async Task<IActionResult> GetDeletion(string operationId, CancellationToken cancellationToken)
    {
        var proof = Request.Headers["X-Account-Deletion-Proof"].ToString();
        if (!Guid.TryParse(operationId, out _) || string.IsNullOrWhiteSpace(proof) || proof.Length < 32)
            return BadRequest(ApiError.Create(HttpContext, 400, "ACCOUNT_DELETION_HEADERS_REQUIRED",
                "Se requiere una prueba de operación válida."));

        var store = HttpContext.RequestServices.GetRequiredService<IAccountDeletionOperationStore>();
        var operation = await store.FindAsync(operationId, AccountDeletionProof.Hash(proof), cancellationToken);
        if (operation is null)
            return NotFound(ApiError.Create(HttpContext, 404, "ACCOUNT_DELETION_NOT_FOUND",
                "Operación no encontrada."));

        return Ok(new
        {
            operationId = operation.OperationId,
            status = operation.Status,
            stage = operation.Stage,
            failureCode = operation.FailureCode
        });
    }
}

public sealed record AccountDeletionUnavailableResponse(ApiError Error, string? OperationId,
    string? State, string? Stage);
