using UTTTGO.Api.Modules.Identity.Contracts;

namespace UTTTGO.Api.Modules.Identity.Application;

// The Media module owns approval, asset ownership and issuance of short-lived private read URLs.
public interface IApprovedAvatarReadAccess
{
    Task<string?> GetApprovedOwnedTemporaryUrlAsync(string mediaAssetId, string ownerId,
        CancellationToken cancellationToken);
}

public interface IOwnProfileService
{
    Task<OwnProfileResult> GetAsync(string token, CancellationToken cancellationToken);
}

public sealed record OwnProfileResult(SessionOutcome Outcome, OwnProfileResponse? Response = null);

public sealed class OwnProfileService(ISessionService session, IApprovedAvatarReadAccess avatar) : IOwnProfileService
{
    public async Task<OwnProfileResult> GetAsync(string token, CancellationToken cancellationToken)
    {
        var authorization = await session.AuthenticateAsync(token, cancellationToken);
        var profile = authorization.Profile;
        if (authorization.Outcome == SessionOutcome.ProfileMissing)
            return new(SessionOutcome.Unauthorized); // API-006 candidate differs from API-004's provisional 404.
        if (authorization.Outcome != SessionOutcome.Success || profile is null)
            return new(authorization.Outcome == SessionOutcome.Success
                ? SessionOutcome.Unavailable : authorization.Outcome);

        return new(SessionOutcome.Success, await OwnProfileProjection.ProjectAsync(profile, avatar,
            cancellationToken));
    }
}

public static class OwnProfileProjection
{
    public static async Task<OwnProfileResponse> ProjectAsync(LocalLoginProfile profile,
        IApprovedAvatarReadAccess avatar, CancellationToken cancellationToken)
    {
        // Without an approved, owned asset Media returns null; never use a stored or client-supplied URL.
        string? avatarUrl = null;
        if (!string.IsNullOrWhiteSpace(profile.AvatarMediaAssetId))
        {
            try
            {
                avatarUrl = await avatar.GetApprovedOwnedTemporaryUrlAsync(profile.AvatarMediaAssetId,
                    profile.Id, cancellationToken);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Media failure cannot make an unmoderated image visible; omit the optional avatar.
            }
        }

        return new OwnProfileResponse(profile.Id, profile.DisplayName!, profile.Email,
            profile.Phone, avatarUrl, profile.Roles.ToArray());
    }
}
