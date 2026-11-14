using UTTTGO.Api.Modules.Catalog.Application;

namespace UTTTGO.Api.Modules.Catalog.Infrastructure;

// Reviews does not exist yet: the catalog never infers a rating from product or seller data.
public sealed class UnconfiguredProductRatingsReader : IProductRatingsReader
{
    public Task<ProductRatingsSnapshot?> GetForProductAsync(string productId,
        CancellationToken cancellationToken) => Task.FromResult<ProductRatingsSnapshot?>(null);
}
