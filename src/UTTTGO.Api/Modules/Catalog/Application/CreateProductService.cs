using UTTTGO.Api.Modules.Catalog.Contracts;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Sellers.Application;
using UTTTGO.Api.Modules.Media.Application;

namespace UTTTGO.Api.Modules.Catalog.Application;

public interface ICreateProductService
{
    Task<CreateProductResult> CreateAsync(string token, CreateProductRequest request,
        CancellationToken cancellationToken);
}

public enum CreateProductOutcome
{
    Created,
    Unauthorized,
    AccountNotActive,
    NotSeller,
    CategoryNotFound,
    ImageNotAvailable,
    ImageNotApproved,
    MediaPromotionFailed,
    RateLimited,
    Unavailable
}

public sealed record CreateProductResult(CreateProductOutcome Outcome, ProductResponse? Response = null);

public sealed class CreateProductService(IAuthenticatedLocalProfileReader profiles,
    ICatalogRateLimitGate rateLimit, ICategoryRepository categories, IProductRepository products,
    ISellerDisplayNames sellers, IProductMediaAccess? media = null) : ICreateProductService
{
    public async Task<CreateProductResult> CreateAsync(string token, CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var authorization = await profiles.ReadAsync(token, cancellationToken);
            if (authorization.Outcome is AuthenticatedLocalProfileOutcome.Unauthorized
                or AuthenticatedLocalProfileOutcome.ProfileMissing)
                return new(CreateProductOutcome.Unauthorized);
            if (authorization.Outcome != AuthenticatedLocalProfileOutcome.Success || authorization.Profile is null)
                return new(CreateProductOutcome.Unavailable);

            var profile = authorization.Profile;
            if (!string.Equals(profile.AccountStatus, "Active", StringComparison.Ordinal))
                return new(CreateProductOutcome.AccountNotActive);
            if (!profile.Roles.Contains("Seller", StringComparer.Ordinal) ||
                string.IsNullOrWhiteSpace(profile.SellerId))
                return new(CreateProductOutcome.NotSeller);

            if (!await rateLimit.IsAllowedForWriteAsync(profile.SellerId, cancellationToken))
                return new(CreateProductOutcome.RateLimited);

            // Client-provided permanent URLs are never accepted; only approved Media references.
            if (!string.IsNullOrWhiteSpace(request.ImageUrl))
                return new(CreateProductOutcome.ImageNotAvailable);
            if (request.MediaAssetId is { } assetId)
            {
                if (media is null || !await media.IsApprovedForOwnerAsync(assetId, profile.Id, cancellationToken))
                    return new(CreateProductOutcome.ImageNotApproved);

                try
                {
                    if (!await media.PromoteApprovedAsync(assetId, profile.Id, cancellationToken))
                        return new(CreateProductOutcome.ImageNotApproved);
                }
                catch (Exception) when (!cancellationToken.IsCancellationRequested)
                {
                    return new(CreateProductOutcome.MediaPromotionFailed);
                }
            }

            var category = await categories.FindByIdAsync(request.CategoryId!, cancellationToken);
            if (category is null || !category.IsActive)
                return new(CreateProductOutcome.CategoryNotFound);

            var ingredients = (request.Ingredients ?? [])
                .Select(item => item.Trim())
                .Where(item => item.Length > 0)
                .ToList();
            var product = await products.CreateAsync(new NewProduct(profile.SellerId,
                request.Name!.Trim(), request.Description!.Trim(), ingredients, category.CategoryId,
                request.Price!.Value, request.Stock!.Value, request.MediaAssetId), cancellationToken);

            // Never trust a repository response that changed ownership or privileged fields.
            if (product.SellerId != profile.SellerId || product.Status != ProductAvailability.Active ||
                product.CategoryId != category.CategoryId || product.Version != 0 ||
                product.MediaAssetId != request.MediaAssetId)
                return new(CreateProductOutcome.Unavailable);

            var sellerNames = await sellers.FindDisplayNamesAsync([product.SellerId], cancellationToken);
            var image = product.MediaAssetId is { } id
                ? await media!.GetApprovedReadUrlAsync(id, cancellationToken) : null;
            if (product.MediaAssetId is not null && image is null)
                return new(CreateProductOutcome.Unavailable);
            var response = ProductProjection.ToResponse(product,
                sellerNames.TryGetValue(product.SellerId, out var displayName) ? displayName : null,
                category.Name, null, image);
            return new(CreateProductOutcome.Created, response);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new(CreateProductOutcome.Unavailable);
        }
    }
}
