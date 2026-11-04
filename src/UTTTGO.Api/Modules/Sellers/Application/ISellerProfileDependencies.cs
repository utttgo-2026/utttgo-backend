namespace UTTTGO.Api.Modules.Sellers.Application;

public interface IPublicSellerRateLimitGate
{
    Task<bool> IsAllowedAsync(string sellerId, CancellationToken cancellationToken);
}

// Only the Sellers module owns this lookup; do not read Users/Catalog/Reviews collections here.
public interface ISellerProfileRepository
{
    Task<SellerProfileSnapshot?> FindByIdAsync(string sellerId, CancellationToken cancellationToken);
}

public sealed record SellerProfileSnapshot(string SellerId, string DisplayName, string Status);

// Reviews owns the ratings. No rating is inferred from seller records or product data.
public interface ISellerRatingsReader
{
    Task<SellerRatingsSnapshot?> GetForSellerAsync(string sellerId, CancellationToken cancellationToken);
}

public sealed record SellerRatingsSnapshot(decimal? Average, long Count);
