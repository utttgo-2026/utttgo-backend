using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace UtttGo.IntegrationTests;

public sealed class OpenApiEndpointTests
{
    [Fact]
    public async Task OpenApiDocumentIsServedInProduction()
    {
        using var factory = Factory();
        using var response = await factory.CreateClient().GetAsync("/openapi/v1.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Contain("json");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("openapi").GetString().Should().StartWith("3.");
        body.RootElement.GetProperty("paths").TryGetProperty("/api/v1/auth/login", out _).Should().BeTrue();
    }

    [Fact]
    public async Task SwaggerUiPageIsServedInProduction()
    {
        using var factory = Factory();
        using var response = await factory.CreateClient().GetAsync("/openapi");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("swagger-ui").And.Contain("/openapi/v1.json");
    }

    private static WebApplicationFactory<Program> Factory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseEnvironment("Production"));
}
