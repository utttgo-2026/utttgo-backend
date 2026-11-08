using UTTTGO.Api.Shared.Infrastructure;

namespace UTTTGO.Api.Shared.Health;

public sealed class RedisHealthRateLimitGate(RedisRateLimiter limiter) : IHealthRateLimitGate
{
    public Task<bool> IsAllowedAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return limiter.IsAllowedAsync(("health", limiter.ClientIp));
    }
}
