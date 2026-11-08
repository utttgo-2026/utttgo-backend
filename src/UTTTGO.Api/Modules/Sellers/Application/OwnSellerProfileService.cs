using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Sellers.Contracts;

namespace UTTTGO.Api.Modules.Sellers.Application;

public interface IOwnSellerProfileService
{
    Task<OwnSellerProfileResult> GetAsync(string token, CancellationToken cancellationToken);
}

public enum OwnSellerProfileOutcome { Found, Unauthorized, NotSeller, RateLimited, Unavailable }

public sealed record OwnSellerProfileResult(OwnSellerProfileOutcome Outcome,
    SellerOwnProfileResponse? Response = null);

public sealed class OwnSellerProfileService(IAuthenticatedLocalProfileReader profiles,
    ISellerProfileWriter sellers, ISellerRateLimitGate rateLimit) : IOwnSellerProfileService
{
    public async Task<OwnSellerProfileResult> GetAsync(string token, CancellationToken cancellationToken)
    {
        try
        {
            var authorization = await profiles.ReadAsync(token, cancellationToken);
            if (authorization.Outcome is AuthenticatedLocalProfileOutcome.Unauthorized
                or AuthenticatedLocalProfileOutcome.ProfileMissing)
                return new(OwnSellerProfileOutcome.Unauthorized);
            if (authorization.Outcome != AuthenticatedLocalProfileOutcome.Success || authorization.Profile is null)
                return new(OwnSellerProfileOutcome.Unavailable);

            var profile = authorization.Profile;
            if (!await rateLimit.IsAllowedForReadAsync(profile.Id, cancellationToken))
                return new(OwnSellerProfileOutcome.RateLimited);

            var seller = await sellers.FindByOwnerAsync(profile.Id, cancellationToken);
            if (seller is null) return new(OwnSellerProfileOutcome.NotSeller);
            return new(OwnSellerProfileOutcome.Found, SellerProjection.ToResponse(seller, profile.Roles));
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new(OwnSellerProfileOutcome.Unavailable);
        }
    }
}
