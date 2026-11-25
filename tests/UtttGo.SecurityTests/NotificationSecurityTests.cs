using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace UtttGo.SecurityTests;

public sealed class NotificationSecurityTests
{
    [Fact]
    public async Task DeviceTokenRegistrationRequiresBearerAuthentication()
    {
        using var factory = HealthSecurityTests.Factory();
        using var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/notifications/device-tokens",
            new { token = "fcm-token", installationId = "install-1", platform = "android" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeviceTokenRemovalRequiresBearerAuthentication()
    {
        using var factory = HealthSecurityTests.Factory();
        using var response = await factory.CreateClient().DeleteAsync(
            "/api/v1/notifications/device-tokens/665f1d408b3f2a0012ab34ce");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
