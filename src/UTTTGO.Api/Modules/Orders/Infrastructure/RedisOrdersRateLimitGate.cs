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

    public Task<bool> IsAllowedForWriteAsync(string userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return limiter.IsAllowedAsync(("order-write", userId));
    }

    public Task<bool> IsAllowedForCancelAsync(string userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return limiter.IsAllowedAsync(("order-cancel", userId));
    }

    public Task<bool> IsAllowedForDeliveryTokenAsync(string userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return limiter.IsAllowedAsync(("order-delivery-token", userId));
    }

    public Task<bool> IsAllowedForDeliveryConfirmAsync(string userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return limiter.IsAllowedAsync(("order-delivery-confirm", userId));
    }
}
