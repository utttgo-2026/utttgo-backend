using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using UTTTGO.Api.Modules.Catalog.Application;
using UTTTGO.Api.Modules.Orders.Application;
using UTTTGO.Api.Modules.Notifications.Application;
using UTTTGO.Api.Modules.Media.Application;

namespace UTTTGO.Api.Modules.Identity.Application;

public interface IDeleteOwnAccountService
{
    Task<DeleteOwnAccountResult> DeleteAsync(string token, CancellationToken cancellationToken);
}

public enum DeleteOwnAccountOutcome { Success, Unauthorized, Conflict, Unavailable }
public sealed record DeleteOwnAccountResult(DeleteOwnAccountOutcome Outcome);

public sealed class DeleteOwnAccountService(IAuthenticatedLocalProfileReader profiles,
    IAccountDeletionRepository accounts, IActiveOrderReader activeOrders, IAccountDeletionCatalog catalog,
    IJwtRevocationStore revocations, ISupabaseSessionRevoker supabase,
    IAccountDeletionMedia media, IDeviceTokenStore? deviceTokens = null) : IDeleteOwnAccountService
{
    public async Task<DeleteOwnAccountResult> DeleteAsync(string token, CancellationToken cancellationToken)
    {
        JwtSecurityToken parsed;
        try { parsed = new JwtSecurityTokenHandler().ReadJwtToken(token); }
        catch (Exception exception) when (exception is ArgumentException or SecurityTokenException)
        { return new(DeleteOwnAccountOutcome.Unauthorized); }

        var subject = parsed.Payload.Sub;
        if (string.IsNullOrWhiteSpace(subject) || parsed.Payload.Expiration is not { } expiration)
            return new(DeleteOwnAccountOutcome.Unauthorized);

        try
        {
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
            if (profile.AccountStatus != "Deleted")
            {
                var deletion = await accounts.DeleteOwnAsync(subject, profile.Id, profile.Version, deletedRef,
                    cancellationToken);
                if (deletion.Outcome == AccountDeletionOutcome.Missing)
                    return new(DeleteOwnAccountOutcome.Unauthorized);
                if (deletion.Outcome == AccountDeletionOutcome.Conflict)
                    return new(DeleteOwnAccountOutcome.Unavailable);
                if (deletion.Profile is not null) profile = deletion.Profile;
            }

            if (!string.IsNullOrWhiteSpace(profile.SellerId))
                await catalog.PauseProductsForSellerAsync(profile.SellerId, cancellationToken);

            await media.EnqueueOwnerDeletionAsync(profile.Id, deletedRef, cancellationToken);

            var ttl = DateTimeOffset.FromUnixTimeSeconds(expiration) - DateTimeOffset.UtcNow;
            if (ttl <= TimeSpan.Zero) return new(DeleteOwnAccountOutcome.Unauthorized);
            if (!await revocations.TryRevokeAsync(JwtRevocationIdentifier.For(parsed, token), ttl,
                    cancellationToken))
                return new(DeleteOwnAccountOutcome.Unavailable);

            try
            {
                if (deviceTokens is not null) await deviceTokens.RemoveForUserAsync(profile.Id, cancellationToken);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested) { }
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
}
