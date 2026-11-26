using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using UTTTGO.Api.Modules.Media.Application;
using UTTTGO.Api.Shared.Infrastructure;

namespace UTTTGO.Api.Modules.Media.Infrastructure;

public sealed class MediaDocument
{
    [BsonId] public ObjectId Id { get; set; }
    [BsonElement("ownerUserId")] public string OwnerUserId { get; set; } = "";
    [BsonElement("objectKey")] public string ObjectKey { get; set; } = "";
    [BsonElement("mimeType")] public string MimeType { get; set; } = "";
    [BsonElement("size")] public long Size { get; set; }
    [BsonElement("status")] public string Status { get; set; } = "";
    [BsonElement("version")] public long Version { get; set; }
    [BsonElement("createdAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; }
    [BsonElement("confirmedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? ConfirmedAt { get; set; }
    [BsonElement("moderationStartedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? ModerationStartedAt { get; set; }
}

public sealed class MongoMediaRepository(MongoStore store) : IMediaRepository
{
    private IMongoCollection<MediaDocument> Assets => store.Database.GetCollection<MediaDocument>("mediaAssets");

    public Task AddAsync(MediaRecord record, CancellationToken cancellationToken) =>
        Assets.InsertOneAsync(new MediaDocument
        {
            Id = ObjectId.Parse(record.Id),
            OwnerUserId = record.OwnerUserId,
            ObjectKey = record.ObjectKey,
            MimeType = record.MimeType,
            Size = record.Size,
            Status = record.Status,
            CreatedAt = record.CreatedAt,
            Version = record.Version
        }, cancellationToken: cancellationToken);

    public async Task<MediaRecord?> FindAsync(string id, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out var objectId)) return null;
        var doc = await Assets.Find(asset => asset.Id == objectId).FirstOrDefaultAsync(cancellationToken);
        return doc is null ? null : new(doc.Id.ToString(), doc.OwnerUserId, doc.ObjectKey, doc.MimeType,
            doc.Size, doc.Status, doc.CreatedAt, doc.ConfirmedAt, doc.Version, doc.ModerationStartedAt);
    }

    public async Task<IReadOnlyList<MediaRecord>> ListForOwnerAsync(string ownerUserId,
        CancellationToken cancellationToken)
    {
        var documents = await Assets.Find(asset => asset.OwnerUserId == ownerUserId &&
            asset.Status != MediaStatus.Deleted).ToListAsync(cancellationToken);
        return documents.Select(ToRecord).ToArray();
    }

    public async Task<bool> TransitionAsync(string id, string ownerId, long version, string from,
        string to, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out var objectId)) return false;
        var filter = Builders<MediaDocument>.Filter.Eq(x => x.Id, objectId) &
            Builders<MediaDocument>.Filter.Eq(x => x.OwnerUserId, ownerId) &
            Builders<MediaDocument>.Filter.Eq(x => x.Version, version) &
            Builders<MediaDocument>.Filter.Eq(x => x.Status, from);
        var update = Builders<MediaDocument>.Update.Set(x => x.Status, to).Inc(x => x.Version, 1);
        if (to == MediaStatus.PendingModeration)
            update = update.Set(x => x.ModerationStartedAt, DateTime.UtcNow);
        if (to is MediaStatus.Approved or MediaStatus.Rejected)
            update = update.Set(x => x.ConfirmedAt, DateTime.UtcNow);
        return (await Assets.UpdateOneAsync(filter, update, cancellationToken: cancellationToken)).ModifiedCount == 1;
    }

    public async Task<bool> PromoteAsync(string id, string ownerId, long version, string sourceKey,
        string targetKey, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out var objectId)) return false;
        var filter = Builders<MediaDocument>.Filter.Eq(x => x.Id, objectId) &
            Builders<MediaDocument>.Filter.Eq(x => x.OwnerUserId, ownerId) &
            Builders<MediaDocument>.Filter.Eq(x => x.Version, version) &
            Builders<MediaDocument>.Filter.Eq(x => x.Status, MediaStatus.Approved) &
            Builders<MediaDocument>.Filter.Eq(x => x.ObjectKey, sourceKey);
        var update = Builders<MediaDocument>.Update.Set(x => x.ObjectKey, targetKey).Inc(x => x.Version, 1);
        return (await Assets.UpdateOneAsync(filter, update, cancellationToken: cancellationToken)).ModifiedCount == 1;
    }

    public async Task<bool> MarkDeletedAsync(string id, string ownerId, long expectedVersion,
        CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out var objectId)) return false;
        var filter = Builders<MediaDocument>.Filter.Eq(x => x.Id, objectId) &
            Builders<MediaDocument>.Filter.Eq(x => x.OwnerUserId, ownerId) &
            Builders<MediaDocument>.Filter.Eq(x => x.Version, expectedVersion) &
            Builders<MediaDocument>.Filter.Ne(x => x.Status, MediaStatus.Deleted);
        var update = Builders<MediaDocument>.Update.Set(x => x.Status, MediaStatus.Deleted)
            .Inc(x => x.Version, 1).Set(x => x.ConfirmedAt, DateTime.UtcNow);
        return (await Assets.UpdateOneAsync(filter, update, cancellationToken: cancellationToken)).ModifiedCount == 1;
    }

    private static MediaRecord ToRecord(MediaDocument document) =>
        new(document.Id.ToString(), document.OwnerUserId, document.ObjectKey, document.MimeType,
            document.Size, document.Status, document.CreatedAt, document.ConfirmedAt, document.Version,
            document.ModerationStartedAt);
}
