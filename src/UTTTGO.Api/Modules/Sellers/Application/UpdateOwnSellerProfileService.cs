using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Sellers.Contracts;

namespace UTTTGO.Api.Modules.Sellers.Application;

public interface IUpdateOwnSellerProfileService
{
    Task<UpdateOwnSellerProfileResult> UpdateAsync(string token, UpdateSellerProfileRequest request,
        CancellationToken cancellationToken);
}

public enum UpdateOwnSellerProfileOutcome
{
    Updated,
    Unauthorized,
    NotSeller,
    Conflict,
    RateLimited,
    Unavailable
}

public sealed record UpdateOwnSellerProfileResult(UpdateOwnSellerProfileOutcome Outcome,
    SellerOwnProfileResponse? Response = null);

public sealed class UpdateOwnSellerProfileService(IAuthenticatedLocalProfileReader profiles,
    ISellerProfileWriter sellers, ISellerRateLimitGate rateLimit) : IUpdateOwnSellerProfileService
{
    public async Task<UpdateOwnSellerProfileResult> UpdateAsync(string token,
        UpdateSellerProfileRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var authorization = await profiles.ReadAsync(token, cancellationToken);
            if (authorization.Outcome is AuthenticatedLocalProfileOutcome.Unauthorized
                or AuthenticatedLocalProfileOutcome.ProfileMissing)
                return new(UpdateOwnSellerProfileOutcome.Unauthorized);
            if (authorization.Outcome != AuthenticatedLocalProfileOutcome.Success || authorization.Profile is null)
                return new(UpdateOwnSellerProfileOutcome.Unavailable);

            var profile = authorization.Profile;
            if (!await rateLimit.IsAllowedForWriteAsync(profile.Id, cancellationToken))
                return new(UpdateOwnSellerProfileOutcome.RateLimited);

            var seller = await sellers.FindByOwnerAsync(profile.Id, cancellationToken);
            if (seller is null) return new(UpdateOwnSellerProfileOutcome.NotSeller);
            if (seller.Status != "Active") return new(UpdateOwnSellerProfileOutcome.NotSeller);

            var storeName = request.HasStoreName ? request.StoreName!.Trim() : null;
            var description = request.HasDescription ? request.Description!.Trim() : null;
            var phone = request.HasPhone ? request.Phone!.Trim() : null;

            var result = await sellers.UpdateOwnAsync(profile.Id, seller.SellerId, seller.Version,
                storeName, request.HasStoreName, description, request.HasDescription,
                phone, request.HasPhone, cancellationToken);
            if (result.Outcome == SellerProfileUpdateOutcome.Missing)
                return new(UpdateOwnSellerProfileOutcome.NotSeller);
            if (result.Outcome == SellerProfileUpdateOutcome.Conflict)
                return new(UpdateOwnSellerProfileOutcome.Conflict);

            var updated = result.Seller;
            // Never trust a repository response that changed ownership or privileged fields.
            if (updated is null || updated.Version <= seller.Version ||
                updated.SellerId != seller.SellerId || updated.OwnerUserId != seller.OwnerUserId ||
                updated.Status != seller.Status ||
                updated.BusinessName != (request.HasStoreName ? storeName : seller.BusinessName) ||
                updated.Description != (request.HasDescription ? description : seller.Description) ||
                updated.PickupPoint != (request.HasPhone ? phone : seller.PickupPoint))
                return new(UpdateOwnSellerProfileOutcome.Unavailable);

            return new(UpdateOwnSellerProfileOutcome.Updated,
                SellerProjection.ToResponse(updated, profile.Roles));
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new(UpdateOwnSellerProfileOutcome.Unavailable);
        }
    }
}
