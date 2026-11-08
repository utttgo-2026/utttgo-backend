using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UTTTGO.Api.Shared.Health;
using Xunit;

namespace UtttGo.IntegrationTests;

public sealed class HealthEndpointTests
{
    [Fact]
    public async Task HealthyDependenciesReturn200WithDependencyStates()
    {
        using var factory = Factory(Healthy());
        using var response = await Get(factory);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("status").GetString().Should().Be("Healthy");
        var checks = body.RootElement.GetProperty("checks");
        checks.GetProperty("mongodb").GetString().Should().Be("Healthy");
        checks.GetProperty("redis").GetString().Should().Be("Healthy");
    }

    [Fact]
    public async Task FailedMongoDependencyReturns503WithErrorContract()
    {
        using var factory = Factory(Healthy(), mongo: new HealthProbeResult(false,
            HealthDependencyOutcome.AuthenticationFailed));
        using var response = await Get(factory);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("status").GetInt32().Should().Be(503);
        body.RootElement.GetProperty("code").GetString().Should().Be("DEPENDENCY_UNAVAILABLE");
        body.RootElement.GetProperty("message").GetString().Should().Be("Servicio temporalmente no disponible.");
        body.RootElement.GetProperty("correlationId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task FailedRedisDependencyReturns503WithErrorContract()
    {
        using var factory = Factory(Healthy(), redis: new HealthProbeResult(false,
            HealthDependencyOutcome.UnreachableOrTimeout));
        using var response = await Get(factory);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).Should().Contain("DEPENDENCY_UNAVAILABLE");
    }

    [Fact]
    public async Task UnhealthyResponseDoesNotRevealWhichDependencyFailed()
    {
        using var factory = Factory(Healthy(), mongo: new HealthProbeResult(false,
            HealthDependencyOutcome.AuthenticationFailed));
        using var response = await Get(factory);

        var payload = await response.Content.ReadAsStringAsync();
        payload.Should().NotContain("mongodb").And.NotContain("redis").And.NotContain("checks");
    }

    [Fact]
    public async Task RateLimitedProbeReturns429WithRetryAfter()
    {
        using var factory = Factory(Healthy(), allowed: false);
        using var response = await Get(factory);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await response.Content.ReadAsStringAsync()).Should().Contain("RATE_LIMITED");
        response.Headers.RetryAfter.Should().NotBeNull();
    }

    [Fact]
    public async Task RateLimiterFailureFailsClosedWith503()
    {
        using var factory = Factory(Healthy(), gateFails: true);
        using var response = await Get(factory);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).Should().Contain("DEPENDENCY_UNAVAILABLE");
    }

    [Fact]
    public async Task UnconfiguredApplicationFailsClosedWith503()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var response = await Get(factory);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    internal static Task<HttpResponseMessage> Get(WebApplicationFactory<Program> factory) =>
        factory.CreateClient().GetAsync("/health");

    internal static WebApplicationFactory<Program> Factory(
        (HealthProbeResult Mongo, HealthProbeResult Redis)? probes = null,
        bool allowed = true, bool gateFails = false,
        HealthProbeResult? mongo = null, HealthProbeResult? redis = null)
    {
        var (mongoResult, redisResult) = probes ?? Healthy();
        if (mongo is not null) mongoResult = mongo;
        if (redis is not null) redisResult = redis;
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IMongoHealthProbe>();
            services.RemoveAll<IRedisHealthProbe>();
            services.RemoveAll<IHealthRateLimitGate>();
            services.AddSingleton<IMongoHealthProbe>(new MongoProbe(mongoResult));
            services.AddSingleton<IRedisHealthProbe>(new RedisProbe(redisResult));
            services.AddSingleton<IHealthRateLimitGate>(new Gate(allowed, gateFails));
        }));
    }

    internal static (HealthProbeResult Mongo, HealthProbeResult Redis) Healthy() =>
        (new HealthProbeResult(true, HealthDependencyOutcome.Healthy),
            new HealthProbeResult(true, HealthDependencyOutcome.Healthy));

    internal sealed class MongoProbe(HealthProbeResult result) : IMongoHealthProbe
    {
        public Task<HealthProbeResult> ProbeAsync(CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    internal sealed class RedisProbe(HealthProbeResult result) : IRedisHealthProbe
    {
        public Task<HealthProbeResult> ProbeAsync(CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    internal sealed class Gate(bool allowed = true, bool fails = false) : IHealthRateLimitGate
    {
        public Task<bool> IsAllowedAsync(CancellationToken cancellationToken) =>
            fails ? throw new InvalidOperationException("internal-secret")
                : Task.FromResult(allowed);
    }
}
