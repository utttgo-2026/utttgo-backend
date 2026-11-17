using UTTTGO.Api.Modules.Sellers.Contracts;

namespace UTTTGO.Api.Modules.Sellers.Application;

// The public contract names the business as storeName and the contact as phone; the
// sellers collection keeps its own names (displayName and pickupPoint).
public static class SellerProjection
{
    public static SellerOwnProfileResponse ToResponse(SellerProfileRecord seller,
        IReadOnlyList<string> roles) =>
        new(seller.SellerId, seller.BusinessName, seller.Description, seller.PickupPoint, seller.Status,
            seller.Status == "Active", roles, seller.CreatedAt, seller.UpdatedAt);
}
