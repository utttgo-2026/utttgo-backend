using UTTTGO.Api.Modules.Admin.Application;
using UTTTGO.Api.Shared.Infrastructure;

namespace UTTTGO.Api.Modules.Admin.Infrastructure;

public sealed class RedisAdminUsersRateLimitGate(RedisRateLimiter limiter) : IAdminUsersRateLimitGate
{
    public Task<bool> IsAllowedAsync(string actorId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return limiter.IsAllowedAsync(("admin-users", actorId));
    }
}
