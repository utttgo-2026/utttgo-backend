namespace UTTTGO.Api.Shared.Health;

public sealed class HealthService(IMongoHealthProbe mongo, IRedisHealthProbe redis, ILogger<HealthService> logger)
    : IHealthService
{
    public async Task<HealthReport> GetAsync(CancellationToken cancellationToken)
    {
        var mongoResult = await mongo.ProbeAsync(cancellationToken);
        var redisResult = await redis.ProbeAsync(cancellationToken);
        var dependencies = new[]
        {
            new HealthDependencyResult(HealthDependency.MongoDb, Status(mongoResult), mongoResult.Outcome),
            new HealthDependencyResult(HealthDependency.Redis, Status(redisResult), redisResult.Outcome)
        };

        var healthy = dependencies.All(dependency => dependency.Outcome == HealthDependencyOutcome.Healthy);
        if (!healthy)
        {
            // Only the failure category is logged: no URIs, credentials or provider messages.
            foreach (var dependency in dependencies.Where(dependency =>
                         dependency.Outcome != HealthDependencyOutcome.Healthy))
                logger.LogWarning("Health probe failed for {Dependency} with category {Category}.",
                    dependency.Name, dependency.Outcome);
        }

        return new HealthReport(healthy, dependencies);
    }

    private static string Status(HealthProbeResult result) =>
        result.Outcome == HealthDependencyOutcome.Healthy ? "Healthy" : "Unhealthy";
}
