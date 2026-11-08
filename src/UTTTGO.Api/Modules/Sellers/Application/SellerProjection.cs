using UTTTGO.Api.Modules.Sellers.Contracts;

namespace UTTTGO.Api.Modules.Sellers.Application;

public static class SellerProjection
{
    public static SellerOwnProfileResponse ToResponse(SellerProfileRecord seller,
        IReadOnlyList<string> roles) =>
        new(seller.SellerId, seller.BusinessName, seller.Description, seller.PickupPoint, seller.Status,
            seller.Status == "Active", roles, seller.CreatedAt, seller.UpdatedAt);
}
