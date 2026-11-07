using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace UTTTGO.Api.Shared.Infrastructure;

public sealed class RedisOptions
{
    public string? Url { get; set; }
}

public sealed class RateLimitPolicy
{
    public int Limit { get; set; }
    public int WindowSeconds { get; set; }
}

public sealed class RateLimitingOptions
{
    public RateLimitPolicy AuthRegister { get; set; } = new();
    public RateLimitPolicy AuthLoginIp { get; set; } = new();
    public RateLimitPolicy AuthLoginEmail { get; set; } = new();
    public RateLimitPolicy PublicSeller { get; set; } = new();
    public RateLimitPolicy AdminUsers { get; set; } = new();
}

public sealed class RedisRateLimiter(IServiceProvider services, IOptionsMonitor<RateLimitingOptions> options,
    IHttpContextAccessor httpContext)
{
    // Each policy is atomic; separate operations also work on Redis Cluster (no cross-slot script).
    private const string Script = """
        local count = redis.call('INCR', KEYS[1])
        if count == 1 then redis.call('EXPIRE', KEYS[1], tonumber(ARGV[2])) end
        if count > tonumber(ARGV[1]) then
            local ttl = redis.call('TTL', KEYS[1])
            return math.max(ttl, 1)
        end
        return 0
        """;

    public string ClientIp => httpContext.HttpContext?.Connection.RemoteIpAddress?.ToString()
        ?? throw new InvalidOperationException("Client IP unavailable for distributed rate limiting.");

    public async Task<bool> IsAllowedAsync(params (string Policy, string Partition)[] partitions)
    {
        if (partitions.Length == 0) throw new InvalidOperationException("Missing rate limit policy.");
        var policies = options.CurrentValue;
        long retry = 0;
        for (var i = 0; i < partitions.Length; i++)
        {
            var (policyName, partition) = partitions[i];
            var policy = policyName switch
            {
                "auth-register" => policies.AuthRegister,
                "auth-login-ip" => policies.AuthLoginIp,
                "auth-login-email" => policies.AuthLoginEmail,
                "public-seller" => policies.PublicSeller,
                "admin-users" => policies.AdminUsers,
                _ => throw new InvalidOperationException("Unknown rate limit policy.")
            };
            if (policy.Limit <= 0 || policy.WindowSeconds <= 0 || string.IsNullOrWhiteSpace(partition))
                throw new InvalidOperationException("Invalid rate limit configuration or partition.");
            // Never store an email or address in plaintext in a Redis key.
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(partition)));
            var key = (RedisKey)$"ratelimit:{policyName}:{hash}";
            var current = (long)await services.GetRequiredService<IConnectionMultiplexer>().GetDatabase()
                .ScriptEvaluateAsync(Script,
                [key], [policy.Limit, policy.WindowSeconds]);
            retry = Math.Max(retry, current);
        }

        if (retry <= 0) return true;
        if (httpContext.HttpContext is { } context)
            context.Response.Headers.RetryAfter = retry.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return false;
    }
}
