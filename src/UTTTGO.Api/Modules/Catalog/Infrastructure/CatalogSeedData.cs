using UTTTGO.Api.Modules.Catalog.Application;

namespace UTTTGO.Api.Modules.Catalog.Infrastructure;

public static class CatalogSeedData
{
    public sealed record SeedCategory(string Name, int SortOrder);

    public static readonly IReadOnlyList<SeedCategory> Categories =
    [
        new("Comida Preparada", 10),
        new("Bebidas", 20),
        new("Snacks", 30)
    ];

    public static string NormalizedName(string name) => TextNormalizer.Normalize(name);
}
