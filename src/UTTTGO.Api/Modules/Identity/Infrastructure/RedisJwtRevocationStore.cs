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
            return await connection.GetDatabase().StringSetAsync(
                $"revoked:{jti}", "1", ttl, When.NotExists);
        }
        catch (RedisException)
        {
            throw new ExternalDependencyUnavailableException();
        }
    }
}
