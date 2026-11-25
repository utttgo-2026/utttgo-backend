using StackExchange.Redis;
using UTTTGO.Api.Modules.Identity.Application;

namespace UTTTGO.Api.Modules.Identity.Infrastructure;

public sealed class RedisAccountSessionRevoker(IConnectionMultiplexer connection) : IAccountSessionRevoker
{
    public async Task RevokeAllAsync(string supabaseUserId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var database = connection.GetDatabase();
            var sessions = await database.HashGetAllAsync($"sessions:{supabaseUserId}");
            foreach (var session in sessions)
            {
                if (!long.TryParse(session.Value.ToString(), out var expiry))
                    throw new ExternalDependencyUnavailableException();
                var ttl = DateTimeOffset.FromUnixTimeSeconds(expiry) - DateTimeOffset.UtcNow;
                if (ttl > TimeSpan.Zero)
                    await database.StringSetAsync($"revoked:{session.Name}", "1", ttl);
            }
            await database.KeyDeleteAsync($"sessions:{supabaseUserId}");
        }
        catch (RedisException) { throw new ExternalDependencyUnavailableException(); }
    }
}
