using MongoDB.Bson;
using MongoDB.Driver;
using UTTTGO.Api.Modules.Sellers.Application;
using UTTTGO.Api.Shared.Infrastructure;

namespace UTTTGO.Api.Modules.Sellers.Infrastructure;

public sealed class MongoSellerProfileRepository(MongoStore store) : ISellerProfileRepository
{
    public async Task<SellerProfileSnapshot?> FindByIdAsync(string sellerId, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(sellerId, out var id)) return null;
        var seller = await store.Database.GetCollection<SellerDocument>("sellers")
            .Find(item => item.Id == id).FirstOrDefaultAsync(cancellationToken);
        return seller is null ? null : new SellerProfileSnapshot(seller.Id.ToString(),
            seller.DisplayName, seller.Status);
    }
}
