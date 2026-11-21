using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Xunit;

namespace UtttGo.IntegrationTests;

public sealed class MediaCatalogOpenApiSnapshotTests
{
    private static readonly string SnapshotPath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..",
        "docs", "openapi", "media-catalog-pr33.json"));

    [Fact]
    public async Task CommittedSnapshotMatchesTheGeneratedApplicationOpenApi()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var generated = JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions
        {
            WriteIndented = true
        }) + "\n";
        if (Environment.GetEnvironmentVariable("UTTTGO_WRITE_OPENAPI_SNAPSHOT") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SnapshotPath)!);
            await File.WriteAllTextAsync(SnapshotPath, generated);
        }

        File.Exists(SnapshotPath).Should().BeTrue("the actual application schema must be versioned");
        var expected = JsonNode.Parse(await File.ReadAllTextAsync(SnapshotPath));
        JsonNode.DeepEquals(JsonNode.Parse(generated), expected).Should().BeTrue(
            "the frontend contract snapshot must match GET /openapi/v1.json");
    }
}
