namespace UTTTGO.Api.Modules.Sellers.Contracts;

public sealed record SellerOwnProfileResponse(string SellerId, string StoreName, string Description,
    string Phone, string Status, bool IsActive, IReadOnlyList<string> Roles,
    DateTime CreatedAt, DateTime UpdatedAt);
