using UTTTGO.Api.Modules.Sellers.Application;
using UTTTGO.Api.Shared.Infrastructure;

namespace UTTTGO.Api.Modules.Sellers.Infrastructure;

public sealed class RedisSellerRateLimitGate(RedisRateLimiter limiter) : ISellerRateLimitGate
{
    public Task<bool> IsAllowedForActivationAsync(string userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return limiter.IsAllowedAsync(("seller-activation", userId));
    }

    public Task<bool> IsAllowedForWriteAsync(string userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return limiter.IsAllowedAsync(("seller-write", userId));
    }

    public Task<bool> IsAllowedForReadAsync(string userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return limiter.IsAllowedAsync(("seller-me", userId));
    }
}
