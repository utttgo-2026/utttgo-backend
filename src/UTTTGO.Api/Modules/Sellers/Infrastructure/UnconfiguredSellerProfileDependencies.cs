using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Sellers.Application;

namespace UTTTGO.Api.Modules.Sellers.Infrastructure;

public sealed class UnconfiguredPublicSellerRateLimitGate : IPublicSellerRateLimitGate
{
    public Task<bool> IsAllowedAsync(string sellerId, CancellationToken cancellationToken) =>
        throw new ExternalDependencyUnavailableException();
}

public sealed class UnconfiguredSellerProfileRepository : ISellerProfileRepository
{
    public Task<SellerProfileSnapshot?> FindByIdAsync(string sellerId, CancellationToken cancellationToken) =>
        throw new ExternalDependencyUnavailableException();
}

public sealed class UnconfiguredSellerRatingsReader : ISellerRatingsReader
{
    public Task<SellerRatingsSnapshot?> GetForSellerAsync(string sellerId, CancellationToken cancellationToken) =>
        Task.FromResult<SellerRatingsSnapshot?>(null);
}
