namespace UTTTGO.Api.Modules.Identity.Application;

// Identity owns the users collection: the adapter must add the Seller role and the seller
// reference without removing or reordering other roles, and without reviving a suspended
// account. The operation must be idempotent.
public interface ISellerRolePromotion
{
    Task<SellerRolePromotionResult> PromoteAsync(string subject, string localUserId, string sellerId,
        CancellationToken cancellationToken);
}

public enum SellerRolePromotionOutcome { Promoted, Missing, Unavailable }

public sealed record SellerRolePromotionResult(SellerRolePromotionOutcome Outcome,
    IReadOnlyList<string>? Roles = null);
