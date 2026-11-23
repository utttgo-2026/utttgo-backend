using Microsoft.Extensions.Options;
using MongoDB.Driver;
using UTTTGO.Api.Shared.Infrastructure;

namespace UTTTGO.Api.Modules.Reviews.Infrastructure;

public sealed class ReviewsMongoIndexes(MongoStore store, IOptions<MongoOptions> options,
    ILogger<ReviewsMongoIndexes> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.Uri)) return;
        try
        {
            var reviews = store.Database.GetCollection<ReviewDocument>("reviews");
            var keys = Builders<ReviewDocument>.IndexKeys;
            await reviews.Indexes.CreateManyAsync([
                new CreateIndexModel<ReviewDocument>(keys.Ascending(item => item.OrderId),
                    new CreateIndexOptions<ReviewDocument> { Name = "orderId_unique", Unique = true }),
                new CreateIndexModel<ReviewDocument>(keys.Ascending(item => item.SellerId)
                    .Descending(item => item.CreatedAt).Ascending(item => item.Id),
                    new CreateIndexOptions<ReviewDocument> { Name = "sellerId_createdAt_id" }),
                new CreateIndexModel<ReviewDocument>(keys.Ascending(item => item.ProductId),
                    new CreateIndexOptions<ReviewDocument> { Name = "productId" })
            ], cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("MongoDB review index creation failed; API startup will continue.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
