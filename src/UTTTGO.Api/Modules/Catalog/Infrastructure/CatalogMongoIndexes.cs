using Microsoft.Extensions.Options;
using MongoDB.Driver;
using UTTTGO.Api.Shared.Infrastructure;

namespace UTTTGO.Api.Modules.Catalog.Infrastructure;

public sealed class CatalogMongoIndexes(MongoStore store, IOptions<MongoOptions> options,
    ILogger<CatalogMongoIndexes> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.Uri)) return;
        try
        {
            var database = store.Database;
            var products = database.GetCollection<ProductDocument>("products");
            var categories = database.GetCollection<CategoryDocument>("categories");
            var productKeys = Builders<ProductDocument>.IndexKeys;
            var categoryKeys = Builders<CategoryDocument>.IndexKeys;

            await products.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<ProductDocument>(productKeys.Ascending(product => product.Status)
                    .Descending(product => product.CreatedAt).Ascending(product => product.Id),
                    new CreateIndexOptions<ProductDocument> { Name = "status_createdAt_desc" }),
                new CreateIndexModel<ProductDocument>(productKeys.Ascending(product => product.SellerId)
                    .Ascending(product => product.Status),
                    new CreateIndexOptions<ProductDocument> { Name = "sellerId_status" }),
                new CreateIndexModel<ProductDocument>(productKeys.Ascending(product => product.CategoryId)
                    .Ascending(product => product.Status),
                    new CreateIndexOptions<ProductDocument> { Name = "categoryId_status" }),
                new CreateIndexModel<ProductDocument>(productKeys.Ascending(product => product.Price)
                    .Ascending(product => product.Status),
                    new CreateIndexOptions<ProductDocument> { Name = "price_status" }),
                new CreateIndexModel<ProductDocument>(productKeys.Ascending(product => product.Status)
                    .Ascending(product => product.NormalizedName).Ascending(product => product.Id),
                    new CreateIndexOptions<ProductDocument> { Name = "status_normalizedName_asc" })
            ], cancellationToken);

            await categories.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<CategoryDocument>(categoryKeys.Ascending(category => category.IsActive)
                    .Ascending(category => category.SortOrder).Ascending(category => category.Name),
                    new CreateIndexOptions<CategoryDocument> { Name = "isActive_sortOrder_name" }),
                new CreateIndexModel<CategoryDocument>(categoryKeys.Ascending(category => category.NormalizedName),
                    new CreateIndexOptions<CategoryDocument> { Name = "normalizedName_unique", Unique = true })
            ], cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("MongoDB index creation failed; API startup will continue.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
