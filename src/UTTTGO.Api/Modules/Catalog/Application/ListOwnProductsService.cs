using UTTTGO.Api.Modules.Catalog.Contracts;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Media.Application;

namespace UTTTGO.Api.Modules.Catalog.Application;

public interface IListOwnProductsService
{
    Task<ListOwnProductsResult> ListAsync(string token, int page, int pageSize,
        CancellationToken cancellationToken);
}

public enum ListOwnProductsOutcome
{
    Success,
    Unauthorized,
    AccountNotActive,
    NotSeller,
    RateLimited,
    Unavailable
}

public sealed record ListOwnProductsResult(ListOwnProductsOutcome Outcome,
    ProductListResponse? Response = null);

public sealed class ListOwnProductsService(IAuthenticatedLocalProfileReader profiles,
    ICatalogRateLimitGate rateLimit, IProductRepository products, ICategoryRepository categories,
    IProductMediaAccess? media = null) : IListOwnProductsService
{
    public async Task<ListOwnProductsResult> ListAsync(string token, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        try
        {
            var authorization = await profiles.ReadAsync(token, cancellationToken);
            if (authorization.Outcome is AuthenticatedLocalProfileOutcome.Unauthorized
                or AuthenticatedLocalProfileOutcome.ProfileMissing)
                return new(ListOwnProductsOutcome.Unauthorized);
            if (authorization.Outcome != AuthenticatedLocalProfileOutcome.Success || authorization.Profile is null)
                return new(ListOwnProductsOutcome.Unavailable);

            var profile = authorization.Profile;
            if (!string.Equals(profile.AccountStatus, "Active", StringComparison.Ordinal))
                return new(ListOwnProductsOutcome.AccountNotActive);
            if (!profile.Roles.Contains("Seller", StringComparer.Ordinal) ||
                string.IsNullOrWhiteSpace(profile.SellerId))
                return new(ListOwnProductsOutcome.NotSeller);
            if (!await rateLimit.IsAllowedForWriteAsync(profile.SellerId, cancellationToken))
                return new(ListOwnProductsOutcome.RateLimited);

            var pageResult = await products.ListForSellerAsync(profile.SellerId, page, pageSize,
                cancellationToken);
            if (pageResult.Items.Count == 0)
                return new(ListOwnProductsOutcome.Success, ProductProjection.ToListResponse([], Query(page, pageSize),
                    pageResult.Total));

            var mediaIds = pageResult.Items.Where(item => item.MediaAssetId is not null)
                .Select(item => item.MediaAssetId!).Distinct(StringComparer.Ordinal).ToList();
            var imageUrls = media is null
                ? new Dictionary<string, MediaReadUrl>(StringComparer.Ordinal)
                : await media.GetApprovedReadUrlsAsync(mediaIds, cancellationToken);
            var items = new List<ProductResponse>(pageResult.Items.Count);
            foreach (var product in pageResult.Items)
            {
                var category = await categories.FindByIdAsync(product.CategoryId, cancellationToken);
                var image = product.MediaAssetId is { } id && imageUrls.TryGetValue(id, out var signed)
                    ? signed : null;
                items.Add(ProductProjection.ToResponse(product, null, category?.Name, null, image));
            }

            return new(ListOwnProductsOutcome.Success,
                ProductProjection.ToListResponse(items, Query(page, pageSize), pageResult.Total));
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new(ListOwnProductsOutcome.Unavailable);
        }
    }

    private static ProductListQuery Query(int page, int pageSize) =>
        new(null, null, null, null, page, pageSize, ProductSort.CreatedAtDesc);
}
