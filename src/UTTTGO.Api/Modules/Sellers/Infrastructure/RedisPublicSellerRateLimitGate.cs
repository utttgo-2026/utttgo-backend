using UTTTGO.Api.Modules.Sellers.Application;
using UTTTGO.Api.Shared.Infrastructure;

namespace UTTTGO.Api.Modules.Sellers.Infrastructure;

public sealed class RedisPublicSellerRateLimitGate(RedisRateLimiter limiter) : IPublicSellerRateLimitGate
{
    public Task<bool> IsAllowedAsync(string sellerId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return limiter.IsAllowedAsync(("public-seller", limiter.ClientIp));
    }
}
