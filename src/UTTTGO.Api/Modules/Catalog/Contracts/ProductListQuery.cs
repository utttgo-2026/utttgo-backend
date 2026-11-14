namespace UTTTGO.Api.Modules.Catalog.Contracts;

// Filters accepted by the public product listing. Names, ranges and sort options are the
// Sprint 2 catalog contract; every value is bounded by the query validator.
public sealed record ProductListQuery(string? Search, string? CategoryId, decimal? MinPrice,
    decimal? MaxPrice, int Page, int PageSize, string SortBy)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;
    public const int MaxPage = 10_000;
    public const int MaxSearchLength = 100;

    // Categories that are active are resolved by the owning module before the query runs.
    // It is never accepted from the client.
    public IReadOnlyList<string>? AllowedCategoryIds { get; init; }
}

public static class ProductSort
{
    public const string CreatedAtDesc = "createdAtDesc";
    public const string PriceAsc = "priceAsc";
    public const string PriceDesc = "priceDesc";
    public const string NameAsc = "nameAsc";

    public static readonly IReadOnlyList<string> Allowed =
        [CreatedAtDesc, PriceAsc, PriceDesc, NameAsc];

    public static bool IsAllowed(string? sortBy) => sortBy is not null && Allowed.Contains(sortBy);
}
