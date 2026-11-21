using UTTTGO.Api.Modules.Catalog.Contracts;
using UTTTGO.Api.Modules.Identity.Application;

namespace UTTTGO.Api.Modules.Catalog.Application;

public interface IUpdateProductService
{
    Task<UpdateProductResult> UpdateAsync(string token, string productId, UpdateProductRequest request,
        CancellationToken cancellationToken);
}

public enum UpdateProductOutcome
{
    Updated,
    Unauthorized,
    AccountNotActive,
    NotSeller,
    NotFound,
    Conflict,
    InvalidImage,
    RateLimited,
    Unavailable
}

public sealed record UpdateProductResult(UpdateProductOutcome Outcome, ProductResponse? Response = null);

public sealed class UpdateProductService(IAuthenticatedLocalProfileReader profiles,
    ICatalogRateLimitGate rateLimit, IProductRepository products, ICategoryRepository categories,
    IProductImageGuard images)
    : IUpdateProductService
{
    public async Task<UpdateProductResult> UpdateAsync(string token, string productId,
        UpdateProductRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var authorization = await profiles.ReadAsync(token, cancellationToken);
            if (authorization.Outcome is AuthenticatedLocalProfileOutcome.Unauthorized
                or AuthenticatedLocalProfileOutcome.ProfileMissing)
                return new(UpdateProductOutcome.Unauthorized);
            if (authorization.Outcome != AuthenticatedLocalProfileOutcome.Success || authorization.Profile is null)
                return new(UpdateProductOutcome.Unavailable);

            var profile = authorization.Profile;
            if (!string.Equals(profile.AccountStatus, "Active", StringComparison.Ordinal))
                return new(UpdateProductOutcome.AccountNotActive);
            if (!profile.Roles.Contains("Seller", StringComparer.Ordinal) ||
                string.IsNullOrWhiteSpace(profile.SellerId))
                return new(UpdateProductOutcome.NotSeller);

            if (!await rateLimit.IsAllowedForWriteAsync(profile.SellerId, cancellationToken))
                return new(UpdateProductOutcome.RateLimited);

            var current = await products.FindByIdAsync(productId, cancellationToken);
            // A product of another seller is reported as missing: no existence is disclosed.
            if (current is null || current.SellerId != profile.SellerId ||
                current.Status == ProductAvailability.Deleted)
                return new(UpdateProductOutcome.NotFound);

            // Same rule as create: a new photo must be a Ready asset of this seller served from
            // the configured S3 host. An explicit null clears the photo and needs no asset.
            var imageUrl = request.HasImageUrl && !string.IsNullOrWhiteSpace(request.ImageUrl)
                ? request.ImageUrl.Trim()
                : null;
            if (request.HasImageUrl && imageUrl is not null &&
                !await images.IsAttachableAsync(imageUrl, profile.Id, cancellationToken))
                return new(UpdateProductOutcome.InvalidImage);

            var changes = new ProductChanges(request.HasName, request.HasName ? request.Name!.Trim() : null,
                request.HasDescription, request.HasDescription ? request.Description!.Trim() : null,
                request.HasIngredients, request.HasIngredients
                    ? request.Ingredients!.Select(item => item.Trim()).Where(item => item.Length > 0).ToList()
                    : null,
                request.HasPrice, request.HasPrice ? request.Price : null,
                request.HasStock, request.HasStock ? request.Stock : null,
                request.HasImageUrl, imageUrl);

            var result = await products.UpdateOwnAsync(profile.SellerId, productId, current.Version,
                changes, cancellationToken);
            if (result.Outcome == ProductUpdateOutcome.Missing) return new(UpdateProductOutcome.NotFound);
            if (result.Outcome == ProductUpdateOutcome.Conflict) return new(UpdateProductOutcome.Conflict);

            var updated = result.Product;
            if (updated is null || updated.Version <= current.Version ||
                updated.SellerId != current.SellerId || updated.Status != current.Status ||
                updated.CategoryId != current.CategoryId)
                return new(UpdateProductOutcome.Unavailable);

            var category = await categories.FindByIdAsync(updated.CategoryId, cancellationToken);
            return new(UpdateProductOutcome.Updated,
                ProductProjection.ToResponse(updated, null, category?.Name, null));
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new(UpdateProductOutcome.Unavailable);
        }
    }
}
