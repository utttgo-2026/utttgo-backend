using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Shared.Infrastructure;

namespace UTTTGO.Api.Modules.Identity.Infrastructure;

public sealed class MongoAccountDeletionOperationStore(MongoStore store) : IAccountDeletionOperationStore
{
    private IMongoCollection<AccountDeletionOperationDocument> Collection =>
        store.Database.GetCollection<AccountDeletionOperationDocument>("accountDeletionOperations");

    public async Task<AccountDeletionOperation?> CreateOrGetAsync(string operationId, string proofHash,
        string subject, string userId, string? sellerId, long expectedVersion, string deletedUserRef,
        string revocationId, long expiration, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var update = Builders<AccountDeletionOperationDocument>.Update.Combine(
            Builders<AccountDeletionOperationDocument>.Update.SetOnInsert(x => x.Id, ObjectId.GenerateNewId()),
            Builders<AccountDeletionOperationDocument>.Update.SetOnInsert(x => x.OperationId, operationId),
            Builders<AccountDeletionOperationDocument>.Update.SetOnInsert(x => x.ProofHash, proofHash),
            Builders<AccountDeletionOperationDocument>.Update.SetOnInsert(x => x.UserId, userId),
            Builders<AccountDeletionOperationDocument>.Update.SetOnInsert(x => x.Subject, subject),
            Builders<AccountDeletionOperationDocument>.Update.SetOnInsert(x => x.SellerId, sellerId),
            Builders<AccountDeletionOperationDocument>.Update.SetOnInsert(x => x.ExpectedVersion, expectedVersion),
            Builders<AccountDeletionOperationDocument>.Update.SetOnInsert(x => x.DeletedUserRef, deletedUserRef),
            Builders<AccountDeletionOperationDocument>.Update.SetOnInsert(x => x.RevocationId, revocationId),
            Builders<AccountDeletionOperationDocument>.Update.SetOnInsert(x => x.Expiration, expiration),
            Builders<AccountDeletionOperationDocument>.Update.SetOnInsert(x => x.Status, AccountDeletionOperationStates.Pending),
            Builders<AccountDeletionOperationDocument>.Update.SetOnInsert(x => x.Stage, AccountDeletionStages.Created),
            Builders<AccountDeletionOperationDocument>.Update.SetOnInsert(x => x.CreatedAt, now),
            Builders<AccountDeletionOperationDocument>.Update.Set(x => x.UpdatedAt, now));
        try
        {
            var item = await Collection.FindOneAndUpdateAsync(
                x => x.OperationId == operationId && x.ProofHash == proofHash, update,
                new FindOneAndUpdateOptions<AccountDeletionOperationDocument>
                { IsUpsert = true, ReturnDocument = ReturnDocument.After }, cancellationToken);
            return ToContract(item);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return await FindAsync(operationId, proofHash, cancellationToken);
        }
    }

    public async Task<AccountDeletionOperation?> FindAsync(string operationId, string proofHash,
        CancellationToken cancellationToken) => ToContract(await Collection.Find(
            x => x.OperationId == operationId && x.ProofHash == proofHash).FirstOrDefaultAsync(cancellationToken));

    public async Task<IReadOnlyList<AccountDeletionOperation>> ListRecoverableAsync(
        CancellationToken cancellationToken)
    {
        var staleBefore = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(1));
        return (await Collection.Find(x =>
            x.Status == AccountDeletionOperationStates.Pending ||
            x.Status == AccountDeletionOperationStates.RequiresIntervention ||
            x.Status == AccountDeletionOperationStates.Processing && x.UpdatedAt <= staleBefore)
            .SortBy(x => x.CreatedAt).Limit(20).ToListAsync(cancellationToken))
            .Select(ToContract).Where(item => item is not null).Select(item => item!).ToArray();
    }

    public async Task<AccountDeletionOperation?> MarkAsync(string operationId, string status, string stage,
        string? failureCode, CancellationToken cancellationToken) => ToContract(await Collection.FindOneAndUpdateAsync(
        x => x.OperationId == operationId,
        Builders<AccountDeletionOperationDocument>.Update.Combine(
            Builders<AccountDeletionOperationDocument>.Update.Set(x => x.Status, status),
            Builders<AccountDeletionOperationDocument>.Update.Set(x => x.Stage, stage),
            Builders<AccountDeletionOperationDocument>.Update.Set(x => x.FailureCode, failureCode),
            Builders<AccountDeletionOperationDocument>.Update.Set(x => x.UpdatedAt, DateTime.UtcNow)),
        new FindOneAndUpdateOptions<AccountDeletionOperationDocument>
        { ReturnDocument = ReturnDocument.After }, cancellationToken));

    public async Task<AccountDeletionOperation?> MarkFailureAsync(string operationId, string status,
        string? failureCode, CancellationToken cancellationToken) => ToContract(await Collection.FindOneAndUpdateAsync(
        x => x.OperationId == operationId,
        Builders<AccountDeletionOperationDocument>.Update.Combine(
            Builders<AccountDeletionOperationDocument>.Update.Set(x => x.Status, status),
            Builders<AccountDeletionOperationDocument>.Update.Set(x => x.FailureCode, failureCode),
            Builders<AccountDeletionOperationDocument>.Update.Set(x => x.UpdatedAt, DateTime.UtcNow)),
        new FindOneAndUpdateOptions<AccountDeletionOperationDocument>
        { ReturnDocument = ReturnDocument.After }, cancellationToken));

    private static AccountDeletionOperation? ToContract(AccountDeletionOperationDocument? item) => item is null
        ? null
        : new(item.OperationId, item.UserId, item.Subject, item.SellerId, item.ExpectedVersion,
            item.DeletedUserRef, item.RevocationId, item.Expiration, item.Status, item.Stage,
            item.FailureCode, item.CreatedAt, item.UpdatedAt);
}

[BsonIgnoreExtraElements]
public sealed class AccountDeletionOperationDocument
{
    [BsonId] public ObjectId Id { get; set; }
    public string OperationId { get; set; } = string.Empty;
    public string ProofHash { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? SellerId { get; set; }
    public long ExpectedVersion { get; set; }
    public string DeletedUserRef { get; set; } = string.Empty;
    public string RevocationId { get; set; } = string.Empty;
    public long Expiration { get; set; }
    public string Status { get; set; } = AccountDeletionOperationStates.Pending;
    public string Stage { get; set; } = AccountDeletionStages.Created;
    public string? FailureCode { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class AccountDeletionOperationIndexes(MongoStore store) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var collection = store.Database.GetCollection<AccountDeletionOperationDocument>("accountDeletionOperations");
            await collection.Indexes.CreateManyAsync([
                new CreateIndexModel<AccountDeletionOperationDocument>(
                    Builders<AccountDeletionOperationDocument>.IndexKeys.Ascending(x => x.OperationId),
                    new CreateIndexOptions { Unique = true, Name = "operation_id_unique" }),
                new CreateIndexModel<AccountDeletionOperationDocument>(
                    Builders<AccountDeletionOperationDocument>.IndexKeys.Ascending(x => x.Status)
                        .Ascending(x => x.UpdatedAt).Ascending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "recovery_queue" })], cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // The owning store will retry reads/writes; startup must remain fail-closed.
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
