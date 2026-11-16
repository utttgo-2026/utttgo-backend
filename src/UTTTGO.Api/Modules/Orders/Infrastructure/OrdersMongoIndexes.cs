using Microsoft.Extensions.Options;
using MongoDB.Driver;
using UTTTGO.Api.Shared.Infrastructure;

namespace UTTTGO.Api.Modules.Orders.Infrastructure;

public sealed class OrdersMongoIndexes(MongoStore store, IOptions<MongoOptions> options,
    ILogger<OrdersMongoIndexes> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.Uri)) return;
        try
        {
            var orders = store.Database.GetCollection<OrderDocument>("orders");
            var logs = store.Database.GetCollection<OrderAuditLogDocument>("orderAuditLogs");
            var orderKeys = Builders<OrderDocument>.IndexKeys;
            var logKeys = Builders<OrderAuditLogDocument>.IndexKeys;

            await orders.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<OrderDocument>(orderKeys.Ascending(item => item.BuyerUserId)
                    .Descending(item => item.CreatedAt),
                    new CreateIndexOptions<OrderDocument> { Name = "buyerUserId_createdAt_desc" }),
                new CreateIndexModel<OrderDocument>(orderKeys.Ascending(item => item.SellerId)
                    .Descending(item => item.CreatedAt),
                    new CreateIndexOptions<OrderDocument> { Name = "sellerId_createdAt_desc" }),
                new CreateIndexModel<OrderDocument>(orderKeys.Ascending(item => item.Status)
                    .Descending(item => item.UpdatedAt),
                    new CreateIndexOptions<OrderDocument> { Name = "status_updatedAt_desc" }),
                new CreateIndexModel<OrderDocument>(orderKeys.Ascending(item => item.BuyerUserId)
                    .Ascending(item => item.IdempotencyKey),
                    new CreateIndexOptions<OrderDocument>
                    {
                        Name = "buyerUserId_idempotencyKey_unique",
                        Unique = true,
                        // Partial: only the orders that carry the header take part.
                        PartialFilterExpression = Builders<OrderDocument>.Filter.Exists(item => item.IdempotencyKey)
                    })
            ], cancellationToken);

            await logs.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<OrderAuditLogDocument>(logKeys.Ascending(item => item.OrderId)
                    .Descending(item => item.OccurredAt),
                    new CreateIndexOptions<OrderAuditLogDocument> { Name = "orderId_occurredAt_desc" }),
                new CreateIndexModel<OrderAuditLogDocument>(logKeys.Ascending(item => item.ActorUserId)
                    .Descending(item => item.OccurredAt),
                    new CreateIndexOptions<OrderAuditLogDocument> { Name = "actorUserId_occurredAt_desc" })
            ], cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("MongoDB index creation failed; API startup will continue.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
