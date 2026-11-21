using UTTTGO.Api.Modules.Catalog.Contracts;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Media.Application;
using UTTTGO.Api.Modules.Sellers.Application;

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
    InvalidImage,
    RateLimited,
    Unavailable
}

public sealed record CreateProductResult(CreateProductOutcome Outcome, ProductResponse? Response = null);

public sealed class CreateProductService(IAuthenticatedLocalProfileReader profiles,
    ICatalogRateLimitGate rateLimit, ICategoryRepository categories, IProductRepository products,
    ISellerDisplayNames sellers, IProductImageGuard images) : ICreateProductService
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

            // A photo is accepted only when Media proves the asset exists, is Ready and belongs
            // to this seller, and the URL is served from the configured S3 host
            // (BR-007, DEC-019.f, RF-V11). Everything else fails closed.
            var imageReference = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim();
            var image = imageReference is null
                ? null
                : await images.ResolveAttachableAsync(imageReference, profile.Id, cancellationToken);
            if (imageReference is not null && image is null)
                return new(CreateProductOutcome.InvalidImage);

            var category = await categories.FindByIdAsync(request.CategoryId!, cancellationToken);
            if (category is null || !category.IsActive)
                return new(CreateProductOutcome.CategoryNotFound);

            var ingredients = (request.Ingredients ?? [])
                .Select(item => item.Trim())
                .Where(item => item.Length > 0)
                .ToList();
            var product = await products.CreateAsync(new NewProduct(profile.SellerId,
                request.Name!.Trim(), request.Description!.Trim(), ingredients, category.CategoryId,
                request.Price!.Value, request.Stock!.Value, image?.AssetId, image?.ObjectKey), cancellationToken);

            // Never trust a repository response that changed ownership or privileged fields.
            if (product.SellerId != profile.SellerId || product.Status != ProductAvailability.Active ||
                product.CategoryId != category.CategoryId || product.Version != 0)
                return new(CreateProductOutcome.Unavailable);

            var sellerNames = await sellers.FindDisplayNamesAsync([product.SellerId], cancellationToken);
            var imageUrl = await images.GetReadUrlAsync(
                product.ImageAssetId is not null && product.ImageObjectKey is not null
                    ? new MediaAssetReference(product.ImageAssetId, product.ImageObjectKey) : null,
                cancellationToken);
            var response = ProductProjection.ToResponse(product,
                sellerNames.TryGetValue(product.SellerId, out var displayName) ? displayName : null,
                category.Name, null, imageUrl);
            return new(CreateProductOutcome.Created, response);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new(CreateProductOutcome.Unavailable);
        }
    }
}
