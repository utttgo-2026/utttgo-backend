using UTTTGO.Api.Modules.Sellers.Contracts;

namespace UTTTGO.Api.Modules.Sellers.Application;

public interface ISellerProfileService
{
    Task<SellerProfileResult> GetAsync(string sellerId, CancellationToken cancellationToken);
}

public enum SellerProfileOutcome { Found, NotFound, RateLimited, Unavailable }

public sealed record SellerProfileResult(SellerProfileOutcome Outcome, SellerProfileResponse? Response = null);

public sealed class SellerProfileService(
    IPublicSellerRateLimitGate rateLimit,
    ISellerProfileRepository sellers,
    ISellerRatingsReader ratings) : ISellerProfileService
{
    public async Task<SellerProfileResult> GetAsync(string sellerId, CancellationToken cancellationToken)
    {
        try
        {
            if (!await rateLimit.IsAllowedAsync(sellerId, cancellationToken))
                return new(SellerProfileOutcome.RateLimited);

            var seller = await sellers.FindByIdAsync(sellerId, cancellationToken);
            if (seller is null || seller.Status is "Deleted" or "Suspended")
                return new(SellerProfileOutcome.NotFound);
            if (seller.SellerId != sellerId || string.IsNullOrWhiteSpace(seller.DisplayName) ||
                seller.Status is not ("Active" or "Inactive"))
                return new(SellerProfileOutcome.Unavailable);

            decimal? average = null;
            long? count = null;
            try
            {
                var review = await ratings.GetForSellerAsync(sellerId, cancellationToken);
                if (review is not null && review.Count >= 0 &&
                    (review.Count == 0 && review.Average is null ||
                     review.Count > 0 && review.Average is >= 0 and <= 5))
                {
                    average = review.Average;
                    count = review.Count;
                }
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Reviews is optional for public profile reads; omit both values on failure.
            }

            return new(SellerProfileOutcome.Found, new SellerProfileResponse(seller.SellerId,
                seller.DisplayName, average, count, seller.Status == "Active"));
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Seller storage and distributed public rate limiting are critical: fail closed.
            return new(SellerProfileOutcome.Unavailable);
        }
    }
}
