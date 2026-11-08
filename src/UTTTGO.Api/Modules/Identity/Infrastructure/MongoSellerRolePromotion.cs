using MongoDB.Bson;
using MongoDB.Driver;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Shared.Infrastructure;

namespace UTTTGO.Api.Modules.Identity.Infrastructure;

public sealed class MongoSellerRolePromotion(MongoStore store) : ISellerRolePromotion
{
    private IMongoCollection<UserDocument> Users => store.Database.GetCollection<UserDocument>("users");

    public async Task<SellerRolePromotionResult> PromoteAsync(string subject, string localUserId,
        string sellerId, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(localUserId, out var id) || !ObjectId.TryParse(sellerId, out _))
            return new(SellerRolePromotionOutcome.Missing);

        var filter = Builders<UserDocument>.Filter.Eq(user => user.Id, id) &
            Builders<UserDocument>.Filter.Eq(user => user.SupabaseUserId, subject);
        // AddToSet keeps the operation idempotent: the role is never duplicated and no other role changes.
        var update = Builders<UserDocument>.Update
            .AddToSet(user => user.Roles, "Seller")
            .Set(user => user.SellerId, sellerId)
            .Inc(user => user.Version, 1);
        var updated = await Users.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<UserDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
        if (updated is null) return new(SellerRolePromotionOutcome.Missing);
        return new(SellerRolePromotionOutcome.Promoted, updated.Roles.ToArray());
    }
}
