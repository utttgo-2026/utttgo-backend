using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace UtttGo.IntegrationTests;

public sealed class NotificationEndpointTests
{
    [Fact]
    public async Task DeviceTokenRegistrationRejectsMissingAuthentication()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/notifications/device-tokens",
            new { token = "fcm-token", installationId = "install-1", platform = "android" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
