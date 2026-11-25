using UTTTGO.Api.Modules.Catalog.Contracts;
using UTTTGO.Api.Shared.Contracts;

namespace UTTTGO.Api.Modules.Catalog.Application;

public interface IListCategoriesService
{
    Task<CategoryListResult> ListAsync(int page, int pageSize, CancellationToken cancellationToken);
}

public enum CategoryListOutcome { Success, RateLimited, Unavailable }

public sealed record CategoryListResult(CategoryListOutcome Outcome, CategoryListResponse? Response = null);

public sealed class ListCategoriesService(ICatalogRateLimitGate rateLimit, ICategoryRepository categories,
    ICatalogCache? cache = null)
    : IListCategoriesService
{
    public async Task<CategoryListResult> ListAsync(int page, int pageSize,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!await rateLimit.IsAllowedForPublicReadAsync(cancellationToken))
                return new(CategoryListOutcome.RateLimited);

            var cached = cache is null ? null : await cache.GetCategoriesAsync(page, pageSize, cancellationToken);
            if (cached is not null) return new(CategoryListOutcome.Success, cached);
            var result = await categories.ListAsync(page, pageSize, cancellationToken);
            var items = result.Items.Select(item => new CategoryResponse(item.CategoryId, item.Name,
                item.SortOrder)).ToList();
            var response = new CategoryListResponse(items, page, pageSize,
                PaginationCount.FromCount(result.Total));
            if (cache is not null) await cache.SetCategoriesAsync(page, pageSize, response, cancellationToken);
            return new(CategoryListOutcome.Success, response);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new(CategoryListOutcome.Unavailable);
        }
    }
}
