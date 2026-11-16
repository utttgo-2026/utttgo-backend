using UTTTGO.Api.Modules.Orders.Application;
using UTTTGO.Api.Shared.Infrastructure;

namespace UTTTGO.Api.Modules.Orders.Infrastructure;

public sealed class RedisOrdersRateLimitGate(RedisRateLimiter limiter) : IOrdersRateLimitGate
{
    public Task<bool> IsAllowedForCreateAsync(string userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return limiter.IsAllowedAsync(("order-create", userId));
    }

    public Task<bool> IsAllowedForReadAsync(string userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return limiter.IsAllowedAsync(("order-read", userId));
    }
}
