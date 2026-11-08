using MongoDB.Bson;
using MongoDB.Driver;
using UTTTGO.Api.Modules.Sellers.Application;
using UTTTGO.Api.Shared.Infrastructure;

namespace UTTTGO.Api.Modules.Sellers.Infrastructure;

public sealed class MongoSellerProfileWriter(MongoStore store) : ISellerProfileWriter
{
    private IMongoCollection<SellerDocument> Sellers => store.Database.GetCollection<SellerDocument>("sellers");

    public async Task<SellerProfileRecord?> FindByOwnerAsync(string ownerUserId,
        CancellationToken cancellationToken)
    {
        var seller = await Sellers.Find(item => item.OwnerUserId == ownerUserId)
            .FirstOrDefaultAsync(cancellationToken);
        return seller is null ? null : ToRecord(seller);
    }

    public async Task<SellerProfileRecord> CreatePendingAsync(string ownerUserId, string businessName,
        string description, string pickupPoint, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var document = new SellerDocument
        {
            Id = ObjectId.GenerateNewId(),
            OwnerUserId = ownerUserId,
            DisplayName = businessName,
            Description = description,
            PickupPoint = pickupPoint,
            Status = "PendingActivation",
            Version = 0,
            CreatedAt = now,
            UpdatedAt = now
        };

        try
        {
            await Sellers.InsertOneAsync(document, cancellationToken: cancellationToken);
            return ToRecord(document);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // A concurrent activation for the same owner won the unique index: reuse its record.
            var existing = await FindByOwnerAsync(ownerUserId, cancellationToken);
            if (existing is null) throw;
            return existing;
        }
    }

    public async Task<SellerProfileRecord?> ActivateAsync(string sellerId, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(sellerId, out var id)) return null;
        var filter = Builders<SellerDocument>.Filter.Eq(item => item.Id, id) &
            Builders<SellerDocument>.Filter.Eq(item => item.Status, "PendingActivation");
        var update = Builders<SellerDocument>.Update
            .Set(item => item.Status, "Active")
            .Inc(item => item.Version, 1)
            .Set(item => item.UpdatedAt, DateTime.UtcNow);
        var updated = await Sellers.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<SellerDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
        return updated is null ? null : ToRecord(updated);
    }

    public async Task<SellerProfileUpdateResult> UpdateOwnAsync(string ownerUserId, string sellerId,
        long expectedVersion, string? businessName, bool changeBusinessName, string? description,
        bool changeDescription, string? pickupPoint, bool changePickupPoint,
        CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(sellerId, out var id)) return new(SellerProfileUpdateOutcome.Missing);
        var filter = Builders<SellerDocument>.Filter.Eq(item => item.Id, id) &
            Builders<SellerDocument>.Filter.Eq(item => item.OwnerUserId, ownerUserId) &
            Builders<SellerDocument>.Filter.Eq(item => item.Version, expectedVersion) &
            Builders<SellerDocument>.Filter.Eq(item => item.Status, "Active");

        var changes = new List<UpdateDefinition<SellerDocument>>
        {
            Builders<SellerDocument>.Update.Inc(item => item.Version, 1),
            Builders<SellerDocument>.Update.Set(item => item.UpdatedAt, DateTime.UtcNow)
        };
        if (changeBusinessName)
            changes.Add(Builders<SellerDocument>.Update.Set(item => item.DisplayName, businessName!));
        if (changeDescription)
            changes.Add(Builders<SellerDocument>.Update.Set(item => item.Description, description!));
        if (changePickupPoint)
            changes.Add(Builders<SellerDocument>.Update.Set(item => item.PickupPoint, pickupPoint!));

        var updated = await Sellers.FindOneAndUpdateAsync(filter, Builders<SellerDocument>.Update.Combine(changes),
            new FindOneAndUpdateOptions<SellerDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
        if (updated is not null) return new(SellerProfileUpdateOutcome.Updated, ToRecord(updated));

        var exists = await Sellers.Find(item => item.Id == id && item.OwnerUserId == ownerUserId)
            .AnyAsync(cancellationToken);
        return new(exists ? SellerProfileUpdateOutcome.Conflict : SellerProfileUpdateOutcome.Missing);
    }

    private static SellerProfileRecord ToRecord(SellerDocument seller) =>
        new(seller.Id.ToString(), seller.OwnerUserId, seller.DisplayName, seller.Description,
            seller.PickupPoint, seller.Status, seller.Version, seller.CreatedAt, seller.UpdatedAt);
}
