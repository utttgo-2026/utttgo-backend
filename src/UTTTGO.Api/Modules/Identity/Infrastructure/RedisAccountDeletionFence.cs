using System.Security.Cryptography;
using StackExchange.Redis;
using UTTTGO.Api.Modules.Identity.Application;

namespace UTTTGO.Api.Modules.Identity.Infrastructure;

public sealed class RedisAccountDeletionFence(IConnectionMultiplexer connection) : IAccountDeletionFence
{
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan AcquisitionTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(25);

    public async Task<IAsyncDisposable?> AcquireAsync(IEnumerable<string> accountKeys,
        CancellationToken cancellationToken)
    {
        var keys = accountKeys.Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (keys.Length == 0) return new FenceLease([]);

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var database = connection.GetDatabase();
        var acquired = new List<RedisKey>();
        try
        {
            foreach (var key in keys)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var redisKey = $"account-deletion-fence:{key}";
                if (!await TakeWithRetryAsync(database, redisKey, token, cancellationToken))
                {
                    await ReleaseAsync(database, acquired, token);
                    return null;
                }
                acquired.Add(redisKey);
            }
            return new FenceLease(() => ReleaseAsync(database, acquired, token));
        }
        catch (RedisException)
        {
            await ReleaseAsync(database, acquired, token);
            throw new ExternalDependencyUnavailableException();
        }
    }

    private static async Task<bool> TakeWithRetryAsync(IDatabase database, RedisKey key, string token,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + AcquisitionTimeout;
        do
        {
            if (await database.LockTakeAsync(key, token, Lease)) return true;
            await Task.Delay(RetryDelay, cancellationToken);
        }
        while (DateTime.UtcNow < deadline);

        return false;
    }

    private static async Task ReleaseAsync(IDatabase database, IEnumerable<RedisKey> keys, string token)
    {
        foreach (var key in keys)
            await database.LockReleaseAsync(key, token);
    }

    private sealed class FenceLease(Func<Task> release) : IAsyncDisposable
    {
        private int released;

        public FenceLease(IEnumerable<RedisKey> keys)
            : this(() => Task.CompletedTask) { }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref released, 1) == 0) await release();
        }
    }
}
