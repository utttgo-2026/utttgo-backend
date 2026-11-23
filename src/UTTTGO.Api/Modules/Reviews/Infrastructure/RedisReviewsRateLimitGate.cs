using UTTTGO.Api.Modules.Reviews.Application;
using UTTTGO.Api.Shared.Infrastructure;

namespace UTTTGO.Api.Modules.Reviews.Infrastructure;

public sealed class RedisReviewsRateLimitGate(RedisRateLimiter limiter,
    IHttpContextAccessor httpContext) : IReviewsRateLimitGate
{
    public Task<bool> IsAllowedForCreateAsync(string userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return limiter.IsAllowedAsync(("review-create", userId));
    }

    public Task<bool> IsAllowedForReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var ip = httpContext.HttpContext?.Connection.RemoteIpAddress?.ToString();
        if (string.IsNullOrWhiteSpace(ip)) throw new InvalidOperationException("Client IP unavailable.");
        return limiter.IsAllowedAsync(("review-read", ip));
    }
}
