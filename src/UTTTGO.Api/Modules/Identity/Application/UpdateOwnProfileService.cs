using UTTTGO.Api.Modules.Identity.Contracts;

namespace UTTTGO.Api.Modules.Identity.Application;

public interface IUpdateOwnProfileService
{
    Task<UpdateOwnProfileResult> UpdateAsync(string token, UpdateOwnProfileRequest request,
        CancellationToken cancellationToken);
}

public enum UpdateOwnProfileOutcome { Success, Unauthorized, Conflict, Unavailable }

public sealed record UpdateOwnProfileResult(UpdateOwnProfileOutcome Outcome, OwnProfileResponse? Response = null);

public sealed class UpdateOwnProfileService(ISessionService session, IProfileUpdateRepository repository,
    IApprovedAvatarReadAccess avatar)
    : IUpdateOwnProfileService
{
    public async Task<UpdateOwnProfileResult> UpdateAsync(string token, UpdateOwnProfileRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var authorization = await session.AuthenticateAsync(token, cancellationToken);
            if (authorization.Outcome is SessionOutcome.Unauthorized or SessionOutcome.ProfileMissing)
                return new(UpdateOwnProfileOutcome.Unauthorized);
            if (authorization.Outcome != SessionOutcome.Success || authorization.Profile is null)
                return new(UpdateOwnProfileOutcome.Unavailable);

            var original = authorization.Profile;
            var displayName = request.HasDisplayName ? request.DisplayName!.Trim() : null;
            var result = await repository.UpdateOwnAsync(original.SupabaseUserId, original.Id,
                original.Version, displayName, request.HasDisplayName, request.Phone, request.HasPhone,
                cancellationToken);
            if (result.Outcome == ProfileUpdateOutcome.Conflict) return new(UpdateOwnProfileOutcome.Conflict);
            if (result.Outcome == ProfileUpdateOutcome.Missing) return new(UpdateOwnProfileOutcome.Unauthorized);

            var updated = result.Profile;
            // Never trust a repository response that changed ownership or privileged fields.
            if (updated is null || updated.Version <= original.Version ||
                updated.Id != original.Id || updated.SupabaseUserId != original.SupabaseUserId ||
                updated.AccountStatus != original.AccountStatus || updated.Email != original.Email ||
                !updated.Roles.SequenceEqual(original.Roles) ||
                updated.AvatarMediaAssetId != original.AvatarMediaAssetId ||
                updated.DisplayName != (request.HasDisplayName ? displayName : original.DisplayName) ||
                updated.Phone != (request.HasPhone ? request.Phone : original.Phone))
                return new(UpdateOwnProfileOutcome.Unavailable);

            return new(UpdateOwnProfileOutcome.Success,
                await OwnProfileProjection.ProjectAsync(updated, avatar, cancellationToken));
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new(UpdateOwnProfileOutcome.Unavailable);
        }
    }
}
