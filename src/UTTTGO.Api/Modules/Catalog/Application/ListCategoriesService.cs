using UTTTGO.Api.Modules.Catalog.Contracts;
using UTTTGO.Api.Shared.Contracts;

namespace UTTTGO.Api.Modules.Catalog.Application;

public interface IListCategoriesService
{
    Task<CategoryListResult> ListAsync(int page, int pageSize, CancellationToken cancellationToken);
}

public enum CategoryListOutcome { Success, RateLimited, Unavailable }

public sealed record CategoryListResult(CategoryListOutcome Outcome, CategoryListResponse? Response = null);

public sealed class ListCategoriesService(ICatalogRateLimitGate rateLimit, ICategoryRepository categories)
    : IListCategoriesService
{
    public async Task<CategoryListResult> ListAsync(int page, int pageSize,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!await rateLimit.IsAllowedForPublicReadAsync(cancellationToken))
                return new(CategoryListOutcome.RateLimited);

            var result = await categories.ListAsync(page, pageSize, cancellationToken);
            var items = result.Items.Select(item => new CategoryResponse(item.CategoryId, item.Name,
                item.SortOrder)).ToList();
            return new(CategoryListOutcome.Success, new CategoryListResponse(items, page, pageSize,
                PaginationCount.FromCount(result.Total)));
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new(CategoryListOutcome.Unavailable);
        }
    }
}
