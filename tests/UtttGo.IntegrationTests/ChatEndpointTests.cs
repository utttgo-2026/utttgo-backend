using System.Net;
using FluentAssertions;
using Xunit;

namespace UtttGo.IntegrationTests;

public sealed class ChatEndpointTests
{
    [Fact]
    public async Task Messages_requires_bearer_authentication()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/orders/665f1d408b3f2a0012ab34ce/messages");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
