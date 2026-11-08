namespace UTTTGO.Api.Modules.Sellers.Contracts;

public sealed record SellerOwnProfileResponse(string SellerId, string BusinessName, string Description,
    string PickupPoint, string Status, bool IsActive, IReadOnlyList<string> Roles,
    DateTime CreatedAt, DateTime UpdatedAt);
