using System.Net;
using FluentAssertions;
using Xunit;

namespace UtttGo.SecurityTests;

public sealed class ChatSecurityTests
{
    [Fact]
    public async Task Message_history_does_not_allow_anonymous_access()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/orders/665f1d408b3f2a0012ab34ce/messages");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
