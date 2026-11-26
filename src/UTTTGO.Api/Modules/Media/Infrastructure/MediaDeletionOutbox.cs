using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using UTTTGO.Api.Modules.Media.Application;
using UTTTGO.Api.Shared.Infrastructure;

namespace UTTTGO.Api.Modules.Media.Infrastructure;

public sealed class MongoMediaDeletionOutbox(MongoStore store) : IAccountDeletionMedia
{
    private IMongoCollection<MediaDeletionOutboxDocument> Collection =>
        store.Database.GetCollection<MediaDeletionOutboxDocument>("mediaDeletionOutbox");

    public Task EnqueueOwnerDeletionAsync(string ownerUserId, string deletionReference,
        CancellationToken cancellationToken) => Collection.UpdateOneAsync(
        item => item.OwnerUserId == ownerUserId && item.DeletionReference == deletionReference,
        Builders<MediaDeletionOutboxDocument>.Update.Combine(
            Builders<MediaDeletionOutboxDocument>.Update.SetOnInsert(x => x.Id, ObjectId.GenerateNewId()),
            Builders<MediaDeletionOutboxDocument>.Update.SetOnInsert(x => x.OwnerUserId, ownerUserId),
            Builders<MediaDeletionOutboxDocument>.Update.SetOnInsert(x => x.DeletionReference, deletionReference),
            Builders<MediaDeletionOutboxDocument>.Update.SetOnInsert(x => x.Status, "Pending"),
            Builders<MediaDeletionOutboxDocument>.Update.SetOnInsert(x => x.Attempts, 0),
            Builders<MediaDeletionOutboxDocument>.Update.SetOnInsert(x => x.CreatedAt, DateTime.UtcNow)),
        new UpdateOptions { IsUpsert = true }, cancellationToken);
}

public sealed class MediaDeletionOutboxDocument
{
    [BsonId] public ObjectId Id { get; set; }
    public string OwnerUserId { get; set; } = string.Empty;
    public string DeletionReference { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public int Attempts { get; set; }
    public string? WorkerId { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class MediaDeletionMongoIndexes(MongoStore store, IOptions<MongoOptions> options,
    ILogger<MediaDeletionMongoIndexes> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.Uri)) return;
        try
        {
            var collection = store.Database.GetCollection<MediaDeletionOutboxDocument>("mediaDeletionOutbox");
            await collection.Indexes.CreateManyAsync([
                new CreateIndexModel<MediaDeletionOutboxDocument>(
                    Builders<MediaDeletionOutboxDocument>.IndexKeys
                        .Ascending(x => x.OwnerUserId).Ascending(x => x.DeletionReference),
                    new CreateIndexOptions { Unique = true, Name = "owner_deletion_unique" }),
                new CreateIndexModel<MediaDeletionOutboxDocument>(
                    Builders<MediaDeletionOutboxDocument>.IndexKeys
                        .Ascending(x => x.Status).Ascending(x => x.NextAttemptAt).Ascending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "dispatch_queue" })], cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Media deletion outbox indexes could not be created; dispatch will retry.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class MediaDeletionOutboxDispatcher(MongoStore store, IMediaRepository media,
    IMediaObjectStore objects, ILogger<MediaDeletionOutboxDispatcher> logger) : BackgroundService
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(30);
    private readonly string workerId = Guid.NewGuid().ToString("N");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await DispatchAsync(stoppingToken); }
            catch (Exception) when (!stoppingToken.IsCancellationRequested)
            { logger.LogWarning("Media deletion outbox dispatch failed; it will be retried."); }

            try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        }
    }

    private async Task DispatchAsync(CancellationToken cancellationToken)
    {
        var collection = store.Database.GetCollection<MediaDeletionOutboxDocument>("mediaDeletionOutbox");
        for (var index = 0; index < 20; index++)
        {
            var item = await ClaimAsync(collection, cancellationToken);
            if (item is null) return;

            try
            {
                var assets = await media.ListForOwnerAsync(item.OwnerUserId, cancellationToken);
                foreach (var asset in assets)
                {
                    var keys = new[]
                    {
                        asset.ObjectKey,
                        $"staging/{item.OwnerUserId}/{asset.Id}",
                        $"approved/{item.OwnerUserId}/{asset.Id}"
                    }.Where(key => !string.IsNullOrWhiteSpace(key))
                        .Distinct(StringComparer.Ordinal);
                    foreach (var key in keys)
                        await objects.DeleteAsync(key, cancellationToken);
                    if (!await media.MarkDeletedAsync(asset.Id, item.OwnerUserId, asset.Version, cancellationToken))
                    {
                        var current = await media.FindAsync(asset.Id, cancellationToken);
                        if (current is not null && current.Status != MediaStatus.Deleted)
                            throw new InvalidOperationException("Media deletion state changed before completion.");
                    }
                }
                await MarkAsync(collection, item.Id, "Completed", cancellationToken);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                await RetryAsync(collection, item, cancellationToken);
            }
        }
    }

    private async Task<MediaDeletionOutboxDocument?> ClaimAsync(
        IMongoCollection<MediaDeletionOutboxDocument> collection, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var filter = Builders<MediaDeletionOutboxDocument>.Filter.And(
            Builders<MediaDeletionOutboxDocument>.Filter.Or(
                Builders<MediaDeletionOutboxDocument>.Filter.Eq(x => x.Status, "Pending"),
                Builders<MediaDeletionOutboxDocument>.Filter.Eq(x => x.Status, "Failed"),
                Builders<MediaDeletionOutboxDocument>.Filter.And(
                    Builders<MediaDeletionOutboxDocument>.Filter.Eq(x => x.Status, "Processing"),
                    Builders<MediaDeletionOutboxDocument>.Filter.Lte(x => x.LeaseUntil, now))),
            Builders<MediaDeletionOutboxDocument>.Filter.Or(
                Builders<MediaDeletionOutboxDocument>.Filter.Eq(x => x.NextAttemptAt, null),
                Builders<MediaDeletionOutboxDocument>.Filter.Lte(x => x.NextAttemptAt, now)));
        var update = Builders<MediaDeletionOutboxDocument>.Update.Combine(
            Builders<MediaDeletionOutboxDocument>.Update.Set(x => x.Status, "Processing"),
            Builders<MediaDeletionOutboxDocument>.Update.Set(x => x.WorkerId, workerId),
            Builders<MediaDeletionOutboxDocument>.Update.Set(x => x.LeaseUntil, now + LeaseDuration),
            Builders<MediaDeletionOutboxDocument>.Update.Inc(x => x.Attempts, 1));
        return await collection.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<MediaDeletionOutboxDocument>
            {
                ReturnDocument = ReturnDocument.After,
                Sort = Builders<MediaDeletionOutboxDocument>.Sort.Ascending(x => x.CreatedAt)
            }, cancellationToken);
    }

    private Task MarkAsync(IMongoCollection<MediaDeletionOutboxDocument> collection, ObjectId id,
        string status, CancellationToken cancellationToken) => collection.UpdateOneAsync(
        x => x.Id == id && x.Status == "Processing" && x.WorkerId == workerId,
        Builders<MediaDeletionOutboxDocument>.Update.Combine(
            Builders<MediaDeletionOutboxDocument>.Update.Set(x => x.Status, status),
            Builders<MediaDeletionOutboxDocument>.Update.Set<string?>(x => x.WorkerId, null),
            Builders<MediaDeletionOutboxDocument>.Update.Set<DateTime?>(x => x.LeaseUntil, null)),
        cancellationToken: cancellationToken);

    private Task RetryAsync(IMongoCollection<MediaDeletionOutboxDocument> collection,
        MediaDeletionOutboxDocument item, CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromSeconds(Math.Min(300, Math.Pow(2, item.Attempts)));
        var status = "Failed";
        return collection.UpdateOneAsync(
            x => x.Id == item.Id && x.Status == "Processing" && x.WorkerId == workerId,
            Builders<MediaDeletionOutboxDocument>.Update.Combine(
                Builders<MediaDeletionOutboxDocument>.Update.Set(x => x.Status, status),
                Builders<MediaDeletionOutboxDocument>.Update.Set(x => x.NextAttemptAt, DateTime.UtcNow + delay),
                Builders<MediaDeletionOutboxDocument>.Update.Set<string?>(x => x.WorkerId, null),
                Builders<MediaDeletionOutboxDocument>.Update.Set<DateTime?>(x => x.LeaseUntil, null)),
            cancellationToken: cancellationToken);
    }
}
