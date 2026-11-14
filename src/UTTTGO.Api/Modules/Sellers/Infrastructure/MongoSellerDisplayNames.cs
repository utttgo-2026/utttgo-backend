using MongoDB.Driver;
using UTTTGO.Api.Modules.Sellers.Application;
using UTTTGO.Api.Shared.Infrastructure;

namespace UTTTGO.Api.Modules.Sellers.Infrastructure;

public sealed class MongoSellerDisplayNames(MongoStore store) : ISellerDisplayNames
{
    public async Task<IReadOnlyDictionary<string, string>> FindDisplayNamesAsync(
        IReadOnlyCollection<string> sellerIds, CancellationToken cancellationToken)
    {
        if (sellerIds.Count == 0) return new Dictionary<string, string>(StringComparer.Ordinal);
        var ids = sellerIds.Distinct(StringComparer.Ordinal).ToList();
        var sellers = store.Database.GetCollection<SellerDocument>("sellers");
        var documents = await sellers.Find(seller => ids.Contains(seller.Id.ToString()) &&
                seller.Status != "Deleted" && seller.Status != "Suspended")
            .ToListAsync(cancellationToken);
        return documents
            .GroupBy(seller => seller.Id.ToString(), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().DisplayName, StringComparer.Ordinal);
    }
}
