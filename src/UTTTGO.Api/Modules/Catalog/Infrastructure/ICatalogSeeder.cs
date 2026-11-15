namespace UTTTGO.Api.Modules.Catalog.Infrastructure;

public interface ICatalogSeeder
{
    Task<IReadOnlyList<string>> SeedAsync(CancellationToken cancellationToken);
}
