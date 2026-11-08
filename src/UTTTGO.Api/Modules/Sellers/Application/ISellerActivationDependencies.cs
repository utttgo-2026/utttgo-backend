namespace UTTTGO.Api.Modules.Sellers.Application;

// The Sellers module owns the sellers collection and never reads or writes Users,
// Catalog, Media, Orders or Reviews collections.
public interface ISellerProfileWriter
{
    Task<SellerProfileRecord?> FindByOwnerAsync(string ownerUserId, CancellationToken cancellationToken);
    Task<SellerProfileRecord> CreatePendingAsync(string ownerUserId, string businessName,
        string description, string pickupPoint, CancellationToken cancellationToken);
    Task<SellerProfileRecord?> ActivateAsync(string sellerId, CancellationToken cancellationToken);
    Task<SellerProfileUpdateResult> UpdateOwnAsync(string ownerUserId, string sellerId, long expectedVersion,
        string? businessName, bool changeBusinessName, string? description, bool changeDescription,
        string? pickupPoint, bool changePickupPoint, CancellationToken cancellationToken);
}

public sealed record SellerProfileRecord(string SellerId, string OwnerUserId, string BusinessName,
    string Description, string PickupPoint, string Status, long Version, DateTime CreatedAt,
    DateTime UpdatedAt);

public enum SellerProfileUpdateOutcome { Updated, Conflict, Missing }

public sealed record SellerProfileUpdateResult(SellerProfileUpdateOutcome Outcome,
    SellerProfileRecord? Seller = null);

// Distributed limits, partitioned by the authenticated user id and always fail-closed.
public interface ISellerRateLimitGate
{
    Task<bool> IsAllowedForActivationAsync(string userId, CancellationToken cancellationToken);
    Task<bool> IsAllowedForWriteAsync(string userId, CancellationToken cancellationToken);
    Task<bool> IsAllowedForReadAsync(string userId, CancellationToken cancellationToken);
}
