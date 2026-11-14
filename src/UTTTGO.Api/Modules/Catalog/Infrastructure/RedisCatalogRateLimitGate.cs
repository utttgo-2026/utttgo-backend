using UTTTGO.Api.Modules.Catalog.Application;
using UTTTGO.Api.Shared.Infrastructure;

namespace UTTTGO.Api.Modules.Catalog.Infrastructure;

public sealed class RedisCatalogRateLimitGate(RedisRateLimiter limiter) : ICatalogRateLimitGate
{
    public Task<bool> IsAllowedForPublicReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return limiter.IsAllowedAsync(("catalog-public", limiter.ClientIp));
    }

    public Task<bool> IsAllowedForWriteAsync(string sellerId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return limiter.IsAllowedAsync(("catalog-write", sellerId));
    }
}
