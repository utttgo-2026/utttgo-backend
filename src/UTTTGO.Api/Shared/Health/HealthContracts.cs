namespace UTTTGO.Api.Shared.Health;

public sealed record HealthResponse(string Status, IReadOnlyDictionary<string, string> Checks);

public enum HealthDependencyOutcome
{
    Healthy,
    UnreachableOrTimeout,
    AuthenticationFailed,
    InvalidConfiguration,
    Unknown
}

public sealed record HealthProbeResult(bool IsHealthy, HealthDependencyOutcome Outcome)
{
    public static HealthProbeResult Healthy { get; } =
        new(true, HealthDependencyOutcome.Healthy);
}

public sealed record HealthDependencyResult(string Name, string Status, HealthDependencyOutcome Outcome);

public sealed record HealthReport(bool IsHealthy, IReadOnlyList<HealthDependencyResult> Dependencies);

public interface IMongoHealthProbe
{
    Task<HealthProbeResult> ProbeAsync(CancellationToken cancellationToken);
}

public interface IRedisHealthProbe
{
    Task<HealthProbeResult> ProbeAsync(CancellationToken cancellationToken);
}

public interface IHealthService
{
    Task<HealthReport> GetAsync(CancellationToken cancellationToken);
}

public interface IHealthRateLimitGate
{
    Task<bool> IsAllowedAsync(CancellationToken cancellationToken);
}
