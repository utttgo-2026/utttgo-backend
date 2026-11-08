using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Sellers.Contracts;

namespace UTTTGO.Api.Modules.Sellers.Application;

public interface ISellerActivationService
{
    Task<SellerActivationResult> ActivateAsync(string token, SellerActivationRequest request,
        CancellationToken cancellationToken);
}

public enum SellerActivationOutcome
{
    Created,
    AlreadyActive,
    Unauthorized,
    ProfileMissing,
    AccountNotActive,
    Conflict,
    RateLimited,
    Unavailable
}

public sealed record SellerActivationResult(SellerActivationOutcome Outcome,
    SellerOwnProfileResponse? Response = null);

public sealed class SellerActivationService(IAuthenticatedLocalProfileReader profiles,
    ISellerProfileWriter sellers, ISellerRolePromotion promotion, ISellerRateLimitGate rateLimit)
    : ISellerActivationService
{
    public async Task<SellerActivationResult> ActivateAsync(string token, SellerActivationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var authorization = await profiles.ReadAsync(token, cancellationToken);
            if (authorization.Outcome == AuthenticatedLocalProfileOutcome.Unauthorized)
                return new(SellerActivationOutcome.Unauthorized);
            if (authorization.Outcome == AuthenticatedLocalProfileOutcome.ProfileMissing)
                return new(SellerActivationOutcome.ProfileMissing);
            if (authorization.Outcome != AuthenticatedLocalProfileOutcome.Success || authorization.Profile is null)
                return new(SellerActivationOutcome.Unavailable);

            var profile = authorization.Profile;
            if (!string.Equals(profile.AccountStatus, "Active", StringComparison.Ordinal))
                return new(SellerActivationOutcome.AccountNotActive);

            if (!await rateLimit.IsAllowedForActivationAsync(profile.Id, cancellationToken))
                return new(SellerActivationOutcome.RateLimited);

            var businessName = request.BusinessName!.Trim();
            var description = request.Description!.Trim();
            var pickupPoint = request.PickupPoint!.Trim();

            var existing = await sellers.FindByOwnerAsync(profile.Id, cancellationToken);
            if (existing is not null && existing.Status is "Suspended" or "Deleted" or "Inactive")
                return new(SellerActivationOutcome.Conflict);

            var created = existing is null;
            var seller = existing ?? await sellers.CreatePendingAsync(profile.Id, businessName,
                description, pickupPoint, cancellationToken);

            // The role is promoted only when it is missing; the promotion itself is idempotent.
            if (!profile.Roles.Contains("Seller", StringComparer.Ordinal) ||
                !string.Equals(profile.SellerId, seller.SellerId, StringComparison.Ordinal))
            {
                var promotionResult = await promotion.PromoteAsync(profile.SupabaseUserId, profile.Id,
                    seller.SellerId, cancellationToken);
                if (promotionResult.Outcome == SellerRolePromotionOutcome.Missing)
                    return new(SellerActivationOutcome.Unauthorized);
                if (promotionResult.Outcome != SellerRolePromotionOutcome.Promoted)
                    return new(SellerActivationOutcome.Unavailable);
            }

            if (seller.Status != "Active")
            {
                var activated = await sellers.ActivateAsync(seller.SellerId, cancellationToken);
                if (activated is null)
                {
                    var current = await sellers.FindByOwnerAsync(profile.Id, cancellationToken);
                    if (current is null) return new(SellerActivationOutcome.Unavailable);
                    if (current.Status is "Suspended" or "Deleted" or "Inactive")
                        return new(SellerActivationOutcome.Conflict);
                    seller = current;
                }
                else
                {
                    seller = activated;
                }
            }

            var roles = profile.Roles.Contains("Seller", StringComparer.Ordinal)
                ? profile.Roles
                : profile.Roles.Concat(["Seller"]).ToArray();
            return new(created ? SellerActivationOutcome.Created : SellerActivationOutcome.AlreadyActive,
                SellerProjection.ToResponse(seller, roles));
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new(SellerActivationOutcome.Unavailable);
        }
    }
}
