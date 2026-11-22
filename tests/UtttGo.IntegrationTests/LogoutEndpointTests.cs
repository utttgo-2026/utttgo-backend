using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UTTTGO.Api.Modules.Identity.Application;
using Xunit;

namespace UtttGo.IntegrationTests;

public sealed class LogoutEndpointTests
{
    [Fact]
    public async Task ValidTokenReturnsNoContent()
    {
        using var factory = Factory(new Stub(LogoutOutcome.Success));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "valid-token");

        using var response = await client.PostAsync("/api/v1/auth/logout", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsByteArrayAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task RevokedOrInvalidTokenReturnsUnauthorized()
    {
        using var factory = Factory(new Stub(LogoutOutcome.Unauthorized));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");

        using var response = await client.PostAsync("/api/v1/auth/logout", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().Contain("UNAUTHORIZED").And.Contain("correlationId");
    }

    [Fact]
    public async Task RedisFailureReturnsServiceUnavailable()
    {
        using var factory = Factory(new Stub(LogoutOutcome.Unavailable));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "valid-token");

        using var response = await client.PostAsync("/api/v1/auth/logout", null);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).Should().Contain("AUTH_UNAVAILABLE").And.Contain("correlationId");
    }

    private static WebApplicationFactory<Program> Factory(ILogoutService service) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ILogoutService>();
                services.AddSingleton(service);
            }));

    private sealed class Stub(LogoutOutcome outcome) : ILogoutService
    {
        public Task<LogoutResult> LogoutAsync(string token, CancellationToken cancellationToken) =>
            Task.FromResult(new LogoutResult(outcome));
    }
}
