using Microsoft.Extensions.Options;
using MongoDB.Driver;
using UTTTGO.Api.Shared.Infrastructure;

namespace UTTTGO.Api.Modules.Sellers.Infrastructure;

public sealed class SellersMongoIndexes(MongoStore store, IOptions<MongoOptions> options,
    ILogger<SellersMongoIndexes> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.Uri)) return;
        try
        {
            var sellers = store.Database.GetCollection<SellerDocument>("sellers");
            var keys = Builders<SellerDocument>.IndexKeys;
            await sellers.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<SellerDocument>(keys.Ascending(item => item.OwnerUserId),
                    new CreateIndexOptions<SellerDocument>
                    {
                        Name = "ownerUserId_unique",
                        Unique = true,
                        // Partial: documents seeded before the owner link do not block the index.
                        PartialFilterExpression = Builders<SellerDocument>.Filter.Exists(item => item.OwnerUserId)
                    }),
                new CreateIndexModel<SellerDocument>(keys.Ascending(item => item.Status)
                    .Descending(item => item.UpdatedAt), new CreateIndexOptions { Name = "status_updatedAt_desc" })
            ], cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("MongoDB index creation failed; API startup will continue.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
