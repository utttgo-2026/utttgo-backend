using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UTTTGO.Api.Modules.Identity.Application;
using Xunit;

namespace UtttGo.SecurityTests;

public sealed class LogoutSecurityTests
{
    [Fact]
    public async Task MissingAuthorizationIsRejected()
    {
        using var factory = Factory(new Stub(LogoutOutcome.Success));

        using var response = await factory.CreateClient().PostAsync("/api/v1/auth/logout", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().Contain("UNAUTHORIZED");
    }

    [Fact]
    public async Task RevokedTokenCannotLogoutAgain()
    {
        using var factory = Factory(new Stub(LogoutOutcome.Unauthorized));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "revoked-token");

        using var response = await client.PostAsync("/api/v1/auth/logout", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RedisUnavailableFailsClosed()
    {
        using var factory = Factory(new Stub(LogoutOutcome.Unavailable));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "valid-token");

        using var response = await client.PostAsync("/api/v1/auth/logout", null);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
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
