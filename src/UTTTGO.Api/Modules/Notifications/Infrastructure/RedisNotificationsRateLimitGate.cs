using UTTTGO.Api.Modules.Notifications.Application;
using UTTTGO.Api.Shared.Infrastructure;
namespace UTTTGO.Api.Modules.Notifications.Infrastructure;

public sealed class RedisNotificationsRateLimitGate(RedisRateLimiter limiter) : INotificationsRateLimitGate
{ public Task<bool> IsAllowedAsync(string userId, CancellationToken ct) => limiter.IsAllowedAsync(("notification-device-token", userId)); }
