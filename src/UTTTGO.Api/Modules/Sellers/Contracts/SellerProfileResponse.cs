namespace UTTTGO.Api.Modules.Sellers.Contracts;

// Public candidate projection only; no contact details or product list.
public sealed record SellerProfileResponse(string SellerId, string DisplayName,
    decimal? RatingAverage, long? RatingCount, bool IsActive);
