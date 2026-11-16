using MongoDB.Bson;
using MongoDB.Driver;
using UTTTGO.Api.Modules.Sellers.Application;
using UTTTGO.Api.Shared.Infrastructure;

namespace UTTTGO.Api.Modules.Sellers.Infrastructure;

public sealed class MongoSellerOrderProfiles(MongoStore store) : ISellerOrderProfiles
{
    public async Task<SellerOrderProfile?> FindOrderProfileAsync(string sellerId,
        CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(sellerId, out var id)) return null;
        var sellers = store.Database.GetCollection<SellerDocument>("sellers");
        var seller = await sellers.Find(item => item.Id == id).FirstOrDefaultAsync(cancellationToken);
        return seller is null
            ? null
            : new SellerOrderProfile(seller.Id.ToString(), seller.Status, seller.PickupPoint);
    }
}
