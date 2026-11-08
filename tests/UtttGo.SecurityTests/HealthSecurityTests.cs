using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UTTTGO.Api.Shared.Health;
using Xunit;

namespace UtttGo.SecurityTests;

public sealed class HealthSecurityTests
{
    [Fact]
    public async Task ProbeIsPublicAndReturnsOnlyDependencyStates()
    {
        using var factory = Factory();
        using var response = await factory.CreateClient().GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadAsStringAsync();
        payload.Should().Contain("\"status\":\"Healthy\"")
            .And.Contain("\"mongodb\":\"Healthy\"")
            .And.Contain("\"redis\":\"Healthy\"");
    }

    [Fact]
    public async Task ProbeNeverLeaksProviderDetailsOrCredentials()
    {
        using var factory = Factory(mongo: new HealthProbeResult(false,
            HealthDependencyOutcome.AuthenticationFailed));
        using var response = await factory.CreateClient().GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var payload = await response.Content.ReadAsStringAsync();
        payload.Should().NotContain("internal-secret")
            .And.NotContain("mongodb+srv")
            .And.NotContain("password")
            .And.NotContain("Authentication")
            .And.NotContain("mongodb")
            .And.NotContain("redis")
            .And.Contain("correlationId");
    }

    [Fact]
    public async Task ProbeDoesNotExposeUserData()
    {
        using var factory = Factory();
        using var response = await factory.CreateClient().GetAsync("/health");

        var payload = await response.Content.ReadAsStringAsync();
        payload.Should().NotContain("email").And.NotContain("token").And.NotContain("roles");
    }

    internal static WebApplicationFactory<Program> Factory(HealthProbeResult? mongo = null)
    {
        var result = mongo ?? new HealthProbeResult(true, HealthDependencyOutcome.Healthy);
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IMongoHealthProbe>();
            services.RemoveAll<IRedisHealthProbe>();
            services.RemoveAll<IHealthRateLimitGate>();
            services.AddSingleton<IMongoHealthProbe>(new MongoProbe(result));
            services.AddSingleton<IRedisHealthProbe>(new RedisProbe(
                new HealthProbeResult(true, HealthDependencyOutcome.Healthy)));
            services.AddSingleton<IHealthRateLimitGate>(new Gate());
        }));
    }

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

    internal sealed class Gate(bool allowed = true) : IHealthRateLimitGate
    {
        public Task<bool> IsAllowedAsync(CancellationToken cancellationToken) =>
            Task.FromResult(allowed);
    }
}
