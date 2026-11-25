using StackExchange.Redis;
using UTTTGO.Api.Modules.Identity.Application;

namespace UTTTGO.Api.Modules.Identity.Infrastructure;

public sealed class RedisActiveSessionRegistry(IConnectionMultiplexer connection) : IActiveSessionRegistry
{
    public async Task RegisterAsync(string userId, string revocationId, long expiresAtUnix,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var database = connection.GetDatabase();
            await database.HashSetAsync($"sessions:{userId}", revocationId, expiresAtUnix);
            var ttl = DateTimeOffset.FromUnixTimeSeconds(expiresAtUnix) - DateTimeOffset.UtcNow;
            if (ttl > TimeSpan.Zero)
            {
                var key = $"sessions:{userId}";
                var currentTtl = await database.KeyTimeToLiveAsync(key);
                if (currentTtl is null || currentTtl < ttl)
                    await database.KeyExpireAsync(key, ttl);
            }
        }
        catch (RedisException) { throw new ExternalDependencyUnavailableException(); }
    }
}
