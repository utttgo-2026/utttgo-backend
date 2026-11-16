using UTTTGO.Api.Modules.Catalog.Contracts;
using UTTTGO.Api.Modules.Identity.Application;

namespace UTTTGO.Api.Modules.Catalog.Application;

public interface IUpdateProductAvailabilityService
{
    Task<UpdateProductAvailabilityResult> UpdateAsync(string token, string productId,
        UpdateProductAvailabilityRequest request, CancellationToken cancellationToken);
}

public enum UpdateProductAvailabilityOutcome
{
    Updated,
    Unauthorized,
    AccountNotActive,
    NotSeller,
    NotFound,
    Conflict,
    RateLimited,
    Unavailable
}

public sealed record UpdateProductAvailabilityResult(UpdateProductAvailabilityOutcome Outcome,
    ProductResponse? Response = null);

public sealed class UpdateProductAvailabilityService(IAuthenticatedLocalProfileReader profiles,
    ICatalogRateLimitGate rateLimit, IProductRepository products, ICategoryRepository categories)
    : IUpdateProductAvailabilityService
{
    public async Task<UpdateProductAvailabilityResult> UpdateAsync(string token, string productId,
        UpdateProductAvailabilityRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var authorization = await profiles.ReadAsync(token, cancellationToken);
            if (authorization.Outcome is AuthenticatedLocalProfileOutcome.Unauthorized
                or AuthenticatedLocalProfileOutcome.ProfileMissing)
                return new(UpdateProductAvailabilityOutcome.Unauthorized);
            if (authorization.Outcome != AuthenticatedLocalProfileOutcome.Success || authorization.Profile is null)
                return new(UpdateProductAvailabilityOutcome.Unavailable);

            var profile = authorization.Profile;
            if (!string.Equals(profile.AccountStatus, "Active", StringComparison.Ordinal))
                return new(UpdateProductAvailabilityOutcome.AccountNotActive);
            if (!profile.Roles.Contains("Seller", StringComparer.Ordinal) ||
                string.IsNullOrWhiteSpace(profile.SellerId))
                return new(UpdateProductAvailabilityOutcome.NotSeller);

            if (!await rateLimit.IsAllowedForWriteAsync(profile.SellerId, cancellationToken))
                return new(UpdateProductAvailabilityOutcome.RateLimited);

            var current = await products.FindByIdAsync(productId, cancellationToken);
            if (current is null || current.SellerId != profile.SellerId ||
                current.Status == ProductAvailability.Deleted)
                return new(UpdateProductAvailabilityOutcome.NotFound);

            var status = request.Status!;
            var result = await products.UpdateAvailabilityAsync(profile.SellerId, productId,
                current.Version, status, cancellationToken);
            if (result.Outcome == ProductUpdateOutcome.Missing)
                return new(UpdateProductAvailabilityOutcome.NotFound);
            if (result.Outcome == ProductUpdateOutcome.Conflict)
                return new(UpdateProductAvailabilityOutcome.Conflict);

            var updated = result.Product;
            // Pausing a publication never changes the stored stock of the product.
            if (updated is null || updated.Version <= current.Version ||
                updated.SellerId != current.SellerId || updated.Stock != current.Stock ||
                updated.Price != current.Price || updated.Status != status)
                return new(UpdateProductAvailabilityOutcome.Unavailable);

            var category = await categories.FindByIdAsync(updated.CategoryId, cancellationToken);
            return new(UpdateProductAvailabilityOutcome.Updated,
                ProductProjection.ToResponse(updated, null, category?.Name, null));
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new(UpdateProductAvailabilityOutcome.Unavailable);
        }
    }
}
