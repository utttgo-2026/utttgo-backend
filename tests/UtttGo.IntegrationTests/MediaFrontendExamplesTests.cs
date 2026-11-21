using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using UTTTGO.Api.Modules.Catalog.Contracts;
using UTTTGO.Api.Modules.Media.Contracts;
using Xunit;

namespace UtttGo.IntegrationTests;

public sealed class MediaFrontendExamplesTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);
    private static readonly string Root = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs"));

    [Fact]
    public async Task ExamplesHaveTheExactResponseKeysOfTheGeneratedOpenApiSchemas()
    {
        using var schema = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(
            Root, "openapi", "media-catalog-pr33.json")));
        var examples = await File.ReadAllTextAsync(Path.Combine(Root, "media-catalog-pr33-frontend.md"));
        AssertResponseKeys(schema, examples, "media-upload-response", "PresignedUrlResponse");
        AssertResponseKeys(schema, examples, "media-confirm-response", "ConfirmResponse");
        AssertResponseKeys(schema, examples, "media-status-response", "MediaAssetResponse");
        AssertResponseKeys(schema, examples, "product-response", "ProductResponse");
        Read(examples, "media-upload-request").Deserialize<PresignedUrlRequest>(WebJson)!.MimeType
            .Should().Be("image/jpeg");
        Read(examples, "media-confirm-request").Deserialize<ConfirmRequest>(WebJson)!.MediaAssetId
            .Should().MatchRegex("^[a-fA-F0-9]{24}$");
        var create = Read(examples, "product-create-request").Deserialize<CreateProductRequest>(WebJson)!;
        create.MediaAssetId.Should().MatchRegex("^[a-fA-F0-9]{24}$");
        create.UnknownFields.Should().BeNull();

        foreach (var tag in new[] { "product-patch-add", "product-patch-replace",
            "product-patch-remove", "product-patch-preserve" })
        {
            UpdateProductRequest.TryParse(Read(examples, tag), out var request).Should().BeTrue(tag);
            request.Should().NotBeNull();
            if (tag == "product-patch-remove")
            {
                request!.HasMediaAssetId.Should().BeTrue();
                request.MediaAssetId.Should().BeNull();
            }
            if (tag == "product-patch-preserve")
                request!.HasMediaAssetId.Should().BeFalse();
        }
    }

    private static JsonElement Read(string markdown, string tag)
    {
        var match = Regex.Match(markdown,
            $@"<!-- contract:{Regex.Escape(tag)} -->\s*```json\s*(.*?)\s*```",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        match.Success.Should().BeTrue($"the {tag} example must be present");
        using var document = JsonDocument.Parse(match.Groups[1].Value);
        return document.RootElement.Clone();
    }

    private static void AssertResponseKeys(JsonDocument schema, string markdown, string tag,
        string schemaName)
    {
        var expected = schema.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty(schemaName).GetProperty("properties")
            .EnumerateObject().Select(property => property.Name);
        Read(markdown, tag).EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo(expected);
    }
}
