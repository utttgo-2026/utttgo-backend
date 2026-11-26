using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using UTTTGO.Api.Modules.Catalog.Application;
using UTTTGO.Api.Modules.Orders.Application;
using UTTTGO.Api.Modules.Notifications.Application;
using UTTTGO.Api.Modules.Media.Application;

namespace UTTTGO.Api.Modules.Identity.Application;

public interface IDeleteOwnAccountService
{
    Task<DeleteOwnAccountResult> DeleteAsync(string token, string operationId, string proof,
        CancellationToken cancellationToken);
}

public enum DeleteOwnAccountOutcome { Success, Unauthorized, Conflict, InvalidRequest, Unavailable }
public sealed record DeleteOwnAccountResult(DeleteOwnAccountOutcome Outcome, AccountDeletionOperation? Operation = null);

public sealed class DeleteOwnAccountService(IAuthenticatedLocalProfileReader profiles,
    IAccountDeletionRepository accounts, IActiveOrderReader activeOrders, IAccountDeletionCatalog catalog,
    IJwtRevocationStore revocations, ISupabaseSessionRevoker supabase,
    IAccountDeletionMedia media, IAccountDeletionOperationStore operations,
    IDeviceTokenStore? deviceTokens = null) : IDeleteOwnAccountService, IAccountDeletionRecovery
{
    public async Task<DeleteOwnAccountResult> DeleteAsync(string token, string operationId, string proof,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(operationId, out _) || string.IsNullOrWhiteSpace(proof) || proof.Length < 32)
            return new(DeleteOwnAccountOutcome.InvalidRequest);

        JwtSecurityToken parsed;
        try { parsed = new JwtSecurityTokenHandler().ReadJwtToken(token); }
        catch (Exception exception) when (exception is ArgumentException or SecurityTokenException)
        { return new(DeleteOwnAccountOutcome.Unauthorized); }

        var subject = parsed.Payload.Sub;
        if (string.IsNullOrWhiteSpace(subject) || parsed.Payload.Expiration is not { } expiration)
            return new(DeleteOwnAccountOutcome.Unauthorized);

        try
        {
            if (!HasRecentAuthentication(parsed))
                return new(DeleteOwnAccountOutcome.Unauthorized);
            var authorization = await profiles.ReadAsync(token, cancellationToken);
            if (authorization.Outcome is AuthenticatedLocalProfileOutcome.Unauthorized or
                AuthenticatedLocalProfileOutcome.ProfileMissing || authorization.Profile is null)
                return new(DeleteOwnAccountOutcome.Unauthorized);
            if (authorization.Outcome != AuthenticatedLocalProfileOutcome.Success)
                return new(DeleteOwnAccountOutcome.Unavailable);

            var profile = authorization.Profile;
            if (profile.AccountStatus is not ("Active" or "Deleted"))
                return new(DeleteOwnAccountOutcome.Unauthorized);
            if (await activeOrders.HasActiveOrdersAsync(profile.Id, profile.SellerId, cancellationToken))
                return new(DeleteOwnAccountOutcome.Conflict);

            if (profile.AccountStatus == "Deleted" && string.IsNullOrWhiteSpace(profile.DeletedUserRef))
                return new(DeleteOwnAccountOutcome.Unavailable);
            var deletedRef = profile.DeletedUserRef ?? Guid.NewGuid().ToString("N");
            var ttl = DateTimeOffset.FromUnixTimeSeconds(expiration) - DateTimeOffset.UtcNow;
            if (ttl <= TimeSpan.Zero) return new(DeleteOwnAccountOutcome.Unauthorized);
            var operation = await operations.CreateOrGetAsync(operationId, AccountDeletionProof.Hash(proof),
                subject, profile.Id, profile.SellerId, profile.Version, deletedRef,
                JwtRevocationIdentifier.For(parsed, token), expiration, cancellationToken);
            if (operation is null || operation.Subject != subject || operation.UserId != profile.Id)
                return new(DeleteOwnAccountOutcome.Unavailable);

            try
            {
                await RunAsync(operation, ttl, cancellationToken);
            }
            catch (ExternalDependencyUnavailableException)
            {
                var failed = await operations.MarkFailureAsync(operation.OperationId,
                    AccountDeletionOperationStates.RequiresIntervention,
                    "DEPENDENCY_UNAVAILABLE", cancellationToken);
                return new(DeleteOwnAccountOutcome.Unavailable, failed ?? operation);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                var failed = await operations.MarkFailureAsync(operation.OperationId,
                    AccountDeletionOperationStates.RequiresIntervention,
                    "RECOVERY_REQUIRED", cancellationToken);
                return new(DeleteOwnAccountOutcome.Unavailable, failed ?? operation);
            }

            try
            {
                await supabase.RevokeGloballyAsync(token, cancellationToken);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Global provider logout is explicitly best effort after local completion.
            }
            return new(DeleteOwnAccountOutcome.Success);
        }
        catch (ExternalDependencyUnavailableException)
        { return new(DeleteOwnAccountOutcome.Unavailable); }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        { return new(DeleteOwnAccountOutcome.Unavailable); }
    }

    public async Task RecoverAsync(AccountDeletionOperation operation, CancellationToken cancellationToken)
    {
        try
        {
            var ttl = DateTimeOffset.FromUnixTimeSeconds(operation.Expiration) - DateTimeOffset.UtcNow;
            if (ttl <= TimeSpan.Zero) return;
            await RunAsync(operation, ttl, cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            await operations.MarkFailureAsync(operation.OperationId, AccountDeletionOperationStates.RequiresIntervention,
                "RECOVERY_REQUIRED", cancellationToken);
        }
    }

    private async Task RunAsync(AccountDeletionOperation operation, TimeSpan ttl,
        CancellationToken cancellationToken)
    {
        if (operation.Stage == AccountDeletionStages.Created)
        {
            if (!await revocations.TryRevokeAsync(operation.RevocationId, ttl, cancellationToken))
                throw new ExternalDependencyUnavailableException();
            operation = await operations.MarkAsync(operation.OperationId, AccountDeletionOperationStates.Processing,
                AccountDeletionStages.SessionRevoked, null, cancellationToken) ?? operation with
                { Stage = AccountDeletionStages.SessionRevoked };
        }

        if (operation.Stage == AccountDeletionStages.SessionRevoked)
        {
            var deletion = await accounts.DeleteOwnAsync(operation.Subject, operation.UserId,
                operation.ExpectedVersion, operation.DeletedUserRef, cancellationToken);
            if (deletion.Outcome is AccountDeletionOutcome.Missing or AccountDeletionOutcome.Conflict)
                throw new ExternalDependencyUnavailableException();
            operation = await operations.MarkAsync(operation.OperationId, AccountDeletionOperationStates.Processing,
                AccountDeletionStages.ProfileDeleted, null, cancellationToken) ?? operation with
                { Stage = AccountDeletionStages.ProfileDeleted };
        }

        if (operation.Stage == AccountDeletionStages.ProfileDeleted && !string.IsNullOrWhiteSpace(operation.SellerId))
        {
            await catalog.PauseProductsForSellerAsync(operation.SellerId, cancellationToken);
            operation = await operations.MarkAsync(operation.OperationId, AccountDeletionOperationStates.Processing,
                AccountDeletionStages.ProductsPaused, null, cancellationToken) ?? operation with
                { Stage = AccountDeletionStages.ProductsPaused };
        }

        if (operation.Stage is AccountDeletionStages.ProfileDeleted or AccountDeletionStages.ProductsPaused)
        {
            await media.EnqueueOwnerDeletionAsync(operation.UserId, operation.DeletedUserRef, cancellationToken);
            try
            {
                if (deviceTokens is not null)
                    await deviceTokens.RemoveForUserAsync(operation.UserId, cancellationToken);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Device-token cleanup is retryable and never makes a locally completed
                // account deletion look unsuccessful after the JWT was revoked.
            }
            await operations.MarkAsync(operation.OperationId, AccountDeletionOperationStates.Completed,
                AccountDeletionStages.Completed, null, cancellationToken);
        }
    }

    private static bool HasRecentAuthentication(JwtSecurityToken token)
    {
        var claim = token.Claims.FirstOrDefault(item => item.Type == "auth_time")?.Value;
        return long.TryParse(claim, out var seconds) &&
            DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(seconds) <= TimeSpan.FromMinutes(15) &&
            DateTimeOffset.FromUnixTimeSeconds(seconds) <= DateTimeOffset.UtcNow.AddMinutes(1);
    }
}

public sealed class AccountDeletionRecoveryWorker(IServiceScopeFactory scopes,
    IAccountDeletionOperationStore operations, ILogger<AccountDeletionRecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                foreach (var operation in await operations.ListRecoverableAsync(stoppingToken))
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<IAccountDeletionRecovery>()
                        .RecoverAsync(operation, stoppingToken);
                }
            }
            catch (Exception) when (!stoppingToken.IsCancellationRequested)
            { logger.LogWarning("Account deletion recovery pass failed; it will retry."); }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
