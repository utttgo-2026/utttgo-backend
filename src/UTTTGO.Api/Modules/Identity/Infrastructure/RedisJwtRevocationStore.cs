using StackExchange.Redis;
using UTTTGO.Api.Modules.Identity.Application;

namespace UTTTGO.Api.Modules.Identity.Infrastructure;

public sealed class RedisJwtRevocationStore(IConnectionMultiplexer connection) : IJwtRevocationStore
{
    public async Task<bool> TryRevokeAsync(string jti, TimeSpan ttl, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var database = connection.GetDatabase();
            var key = $"revoked:{jti}";
            if (await database.KeyExistsAsync(key)) return true;
            return await database.StringSetAsync(key, "1", ttl, When.NotExists) ||
                await database.KeyExistsAsync(key);
        }
        catch (RedisException)
        {
            throw new ExternalDependencyUnavailableException();
        }
    }
}
