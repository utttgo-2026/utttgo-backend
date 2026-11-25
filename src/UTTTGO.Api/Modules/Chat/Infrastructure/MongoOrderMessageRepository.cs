using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Microsoft.Extensions.Options;
using UTTTGO.Api.Modules.Chat.Application;
using UTTTGO.Api.Shared.Infrastructure;

namespace UTTTGO.Api.Modules.Chat.Infrastructure;

public sealed class OrderMessageDocument
{
    [BsonId] public ObjectId Id { get; set; }
    [BsonElement("orderId")] public string OrderId { get; set; } = string.Empty;
    [BsonElement("authorId")] public string AuthorId { get; set; } = string.Empty;
    [BsonElement("content")] public string Content { get; set; } = string.Empty;
    [BsonElement("createdAt"), BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime CreatedAt { get; set; }
    [BsonElement("idempotencyKey")] public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed class MongoOrderMessageRepository(MongoStore store) : IOrderMessageRepository
{
    private IMongoCollection<OrderMessageDocument> Collection => store.Database.GetCollection<OrderMessageDocument>("orderMessages");
    public async Task<OrderMessageRecord?> FindByIdempotencyKeyAsync(string orderId, string authorId, string key, CancellationToken ct)
    {
        var d = await Collection.Find(x => x.OrderId == orderId && x.AuthorId == authorId && x.IdempotencyKey == key).FirstOrDefaultAsync(ct);
        return d is null ? null : ToRecord(d);
    }
    public async Task<OrderMessageRecord> InsertAsync(OrderMessageRecord message, CancellationToken ct)
    {
        var d = new OrderMessageDocument { Id = ObjectId.GenerateNewId(), OrderId = message.OrderId, AuthorId = message.AuthorId, Content = message.Content, CreatedAt = message.CreatedAt, IdempotencyKey = message.IdempotencyKey };
        try { await Collection.InsertOneAsync(d, cancellationToken: ct); }
        catch (MongoWriteException e) when (e.WriteError?.Category == ServerErrorCategory.DuplicateKey) { throw new DuplicateOrderMessageException(); }
        return ToRecord(d);
    }
    public async Task<(IReadOnlyList<OrderMessageRecord> Items, long Total)> ListAsync(string orderId, int page, int pageSize, CancellationToken ct)
    {
        var filter = Builders<OrderMessageDocument>.Filter.Eq(x => x.OrderId, orderId);
        var total = await Collection.CountDocumentsAsync(filter, cancellationToken: ct);
        var items = await Collection.Find(filter).Sort(Builders<OrderMessageDocument>.Sort.Ascending(x => x.CreatedAt).Ascending(x => x.Id)).Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(ct);
        return (items.Select(ToRecord).ToList(), total);
    }
    private static OrderMessageRecord ToRecord(OrderMessageDocument d) => new(d.Id.ToString(), d.OrderId, d.AuthorId, d.Content, d.CreatedAt, d.IdempotencyKey);
}

public sealed class ChatMongoIndexes(MongoStore store, IOptions<MongoOptions> options,
    ILogger<ChatMongoIndexes> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.Value.Uri)) return;
        try
        {
            var c = store.Database.GetCollection<OrderMessageDocument>("orderMessages");
            await c.Indexes.CreateManyAsync([
                new CreateIndexModel<OrderMessageDocument>(Builders<OrderMessageDocument>.IndexKeys
                    .Ascending(x => x.OrderId).Ascending(x => x.CreatedAt).Ascending(x => x.Id)),
                new CreateIndexModel<OrderMessageDocument>(Builders<OrderMessageDocument>.IndexKeys
                    .Ascending(x => x.OrderId).Ascending(x => x.AuthorId).Ascending(x => x.IdempotencyKey),
                    new CreateIndexOptions { Unique = true })], cancellationToken: ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Chat MongoDB index creation failed; API startup will continue.");
        }
    }
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
