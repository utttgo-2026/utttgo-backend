using UTTTGO.Api.Modules.Chat.Application;
using UTTTGO.Api.Shared.Infrastructure;
namespace UTTTGO.Api.Modules.Chat.Infrastructure;

public sealed class RedisChatRateLimitGate(RedisRateLimiter limiter) : IChatRateLimitGate
{
    public Task<bool> IsAllowedAsync(string userId, bool write, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return CheckAsync(userId, write, ct);
    }

    private async Task<bool> CheckAsync(string userId, bool write, CancellationToken ct)
    {
        try { return await limiter.IsAllowedAsync((write ? "order-write" : "order-read", $"chat:{userId}")); }
        catch (Exception) when (!ct.IsCancellationRequested) { return false; }
    }
}
