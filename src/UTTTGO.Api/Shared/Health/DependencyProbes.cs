using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using StackExchange.Redis;
using UTTTGO.Api.Shared.Infrastructure;

namespace UTTTGO.Api.Shared.Health;

public static class HealthDependency
{
    public const string MongoDb = "mongodb";
    public const string Redis = "redis";
}

public sealed class MongoHealthProbe(IServiceProvider services, IOptions<MongoOptions> options)
    : IMongoHealthProbe
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);

    public async Task<HealthProbeResult> ProbeAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(options.Value.Uri))
                return new HealthProbeResult(false, HealthDependencyOutcome.InvalidConfiguration);

            var client = services.GetRequiredService<IMongoClient>();
            var database = client.GetDatabase(options.Value.Database);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(ProbeTimeout);
            await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1),
                cancellationToken: timeout.Token);
            return new HealthProbeResult(true, HealthDependencyOutcome.Healthy);
        }
        catch (Exception exception)
        {
            return new HealthProbeResult(false, DependencyFailureClassifier.Classify(exception));
        }
    }
}

public sealed class RedisHealthProbe(IServiceProvider services, IOptions<RedisOptions> options)
    : IRedisHealthProbe
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);

    public async Task<HealthProbeResult> ProbeAsync(CancellationToken cancellationToken)
    {
        try
        {
            var url = options.Value.Url;
            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                uri.Scheme is not ("redis" or "rediss") || uri.Host.Length == 0)
                return new HealthProbeResult(false, HealthDependencyOutcome.InvalidConfiguration);

            var connections = services.GetRequiredService<IConnectionMultiplexer>();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(ProbeTimeout);
            await connections.GetDatabase().PingAsync().WaitAsync(ProbeTimeout, timeout.Token);
            return new HealthProbeResult(true, HealthDependencyOutcome.Healthy);
        }
        catch (Exception exception)
        {
            return new HealthProbeResult(false, DependencyFailureClassifier.Classify(exception));
        }
    }
}

public static class DependencyFailureClassifier
{
    private const string RedisNoAuthPrefix = "NOAUTH";

    // StackExchange.Redis reports an authentication failure as a server error; only the
    // category is derived from it, never the provider message.
    private static bool RedisAuthenticationFailure(RedisServerException server) =>
        server.Message.StartsWith(RedisNoAuthPrefix, StringComparison.OrdinalIgnoreCase);

    public static HealthDependencyOutcome Classify(Exception exception) => exception switch
    {
        MongoAuthenticationException => HealthDependencyOutcome.AuthenticationFailed,
        MongoCommandException command when command.Code == 18 => HealthDependencyOutcome.AuthenticationFailed,
        MongoConfigurationException => HealthDependencyOutcome.InvalidConfiguration,
        MongoExecutionTimeoutException => HealthDependencyOutcome.UnreachableOrTimeout,
        MongoConnectionException => HealthDependencyOutcome.UnreachableOrTimeout,
        RedisServerException server when RedisAuthenticationFailure(server) =>
            HealthDependencyOutcome.AuthenticationFailed,
        RedisConnectionException => HealthDependencyOutcome.UnreachableOrTimeout,
        RedisTimeoutException => HealthDependencyOutcome.UnreachableOrTimeout,
        TimeoutException => HealthDependencyOutcome.UnreachableOrTimeout,
        OperationCanceledException => HealthDependencyOutcome.UnreachableOrTimeout,
        _ => HealthDependencyOutcome.Unknown
    };
}
