namespace UTTTGO.Api.Modules.Sellers.Application;

// Published by Sellers so that Orders never reads the sellers collection: only the
// owning module knows how a seller is stored. No contact data is exposed.
public interface ISellerOrderProfiles
{
    Task<SellerOrderProfile?> FindOrderProfileAsync(string sellerId, CancellationToken cancellationToken);
}

public sealed record SellerOrderProfile(string SellerId, string Status, string PickupPoint,
    string OwnerUserId = "");
