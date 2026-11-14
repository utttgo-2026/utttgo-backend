namespace UTTTGO.Api.Modules.Sellers.Application;

// The Sellers module owns the sellers collection. Other modules read it only through this
// published contract; they never query the collection themselves.
public interface ISellerDisplayNames
{
    Task<IReadOnlyDictionary<string, string>> FindDisplayNamesAsync(IReadOnlyCollection<string> sellerIds,
        CancellationToken cancellationToken);
}
