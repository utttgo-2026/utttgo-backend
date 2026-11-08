using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using UTTTGO.Api.Shared.Health;
using Xunit;

namespace UtttGo.UnitTests;

public sealed class HealthServiceTests
{
    [Fact]
    public async Task HealthyDependenciesProduceHealthyReport()
    {
        var service = new HealthService(new MongoProbe(HealthProbeResult.Healthy), new RedisProbe(HealthProbeResult.Healthy),
            NullLogger<HealthService>.Instance);

        var report = await service.GetAsync(CancellationToken.None);

        report.IsHealthy.Should().BeTrue();
        report.Dependencies.Should().HaveCount(2);
        report.Dependencies.Select(dependency => dependency.Name).Should()
            .BeEquivalentTo(["mongodb", "redis"]);
        report.Dependencies.Should().OnlyContain(dependency => dependency.Status == "Healthy");
    }

    [Fact]
    public async Task FailedMongoDependencyMakesReportUnhealthy()
    {
        var service = new HealthService(
            new MongoProbe(new HealthProbeResult(false, HealthDependencyOutcome.AuthenticationFailed)),
            new RedisProbe(HealthProbeResult.Healthy), NullLogger<HealthService>.Instance);

        var report = await service.GetAsync(CancellationToken.None);

        report.IsHealthy.Should().BeFalse();
        var mongo = report.Dependencies.Single(dependency => dependency.Name == "mongodb");
        mongo.Status.Should().Be("Unhealthy");
        mongo.Outcome.Should().Be(HealthDependencyOutcome.AuthenticationFailed);
        report.Dependencies.Single(dependency => dependency.Name == "redis").Status.Should().Be("Healthy");
    }

    [Fact]
    public async Task FailedRedisDependencyMakesReportUnhealthy()
    {
        var service = new HealthService(new MongoProbe(HealthProbeResult.Healthy),
            new RedisProbe(new HealthProbeResult(false, HealthDependencyOutcome.UnreachableOrTimeout)),
            NullLogger<HealthService>.Instance);

        var report = await service.GetAsync(CancellationToken.None);

        report.IsHealthy.Should().BeFalse();
        report.Dependencies.Single(dependency => dependency.Name == "redis").Outcome.Should()
            .Be(HealthDependencyOutcome.UnreachableOrTimeout);
    }

    [Theory]
    [InlineData(HealthDependencyOutcome.UnreachableOrTimeout)]
    [InlineData(HealthDependencyOutcome.AuthenticationFailed)]
    [InlineData(HealthDependencyOutcome.InvalidConfiguration)]
    [InlineData(HealthDependencyOutcome.Unknown)]
    public async Task AnyFailureCategoryIsReportedAsUnhealthy(HealthDependencyOutcome outcome)
    {
        var service = new HealthService(new MongoProbe(new HealthProbeResult(false, outcome)),
            new RedisProbe(HealthProbeResult.Healthy), NullLogger<HealthService>.Instance);

        var report = await service.GetAsync(CancellationToken.None);

        report.IsHealthy.Should().BeFalse();
        report.Dependencies.Single(dependency => dependency.Name == "mongodb").Outcome.Should().Be(outcome);
    }

    [Fact]
    public void ClassifierMapsProviderFailuresToCategories()
    {
        DependencyFailureClassifier.Classify(new TimeoutException()).Should()
            .Be(HealthDependencyOutcome.UnreachableOrTimeout);
        DependencyFailureClassifier.Classify(new OperationCanceledException()).Should()
            .Be(HealthDependencyOutcome.UnreachableOrTimeout);
        DependencyFailureClassifier.Classify(new InvalidOperationException()).Should()
            .Be(HealthDependencyOutcome.Unknown);
    }

    [Theory]
    [InlineData(13)]
    [InlineData(18)]
    public void ClassifierTreatsAuthorizationAndAuthenticationCodesAsAuthenticationFailures(int code)
    {
        DependencyFailureClassifier.ClassifyCommandCode(code).Should()
            .Be(HealthDependencyOutcome.AuthenticationFailed);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(26)]
    [InlineData(11000)]
    public void ClassifierTreatsOtherCommandCodesAsUnknown(int code)
    {
        DependencyFailureClassifier.ClassifyCommandCode(code).Should()
            .Be(HealthDependencyOutcome.Unknown);
    }

    private sealed class MongoProbe(HealthProbeResult result) : IMongoHealthProbe
    {
        public Task<HealthProbeResult> ProbeAsync(CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    private sealed class RedisProbe(HealthProbeResult result) : IRedisHealthProbe
    {
        public Task<HealthProbeResult> ProbeAsync(CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }
}
