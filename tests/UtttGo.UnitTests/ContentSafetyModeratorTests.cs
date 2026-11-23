using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using UTTTGO.Api.Modules.Media.Application;
using UTTTGO.Api.Modules.Media.Infrastructure;
using Xunit;

namespace UtttGo.UnitTests;

public sealed class ContentSafetyModeratorTests
{
    [Fact]
    public async Task MissingPolicyNeverReadsS3OrApproves()
    {
        var images = new Images();
        var analyzer = new Analyzer();
        var moderator = new ContentSafetyImageModerator(images, analyzer, Options.Create(
            new ContentSafetyOptions { MaxHateSeverity = 0 }));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            moderator.ReviewAsync("staging/asset", "image/png", 12, default));
        images.Reads.Should().Be(0);
        analyzer.Calls.Should().Be(0);
    }

    [Fact]
    public async Task SeverityOutsideFourLevelPolicyFailsBeforeReadingImage()
    {
        var images = new Images();
        var policy = TestPolicy();
        policy.MaxHateSeverity = 1;
        var moderator = new ContentSafetyImageModerator(images, new Analyzer(), Options.Create(policy));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            moderator.ReviewAsync("staging/asset", "image/png", 12, default));
        images.Reads.Should().Be(0);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(2, false)]
    public async Task OnlyAnExplicitCompleteTestPolicyCanClassifyAnImage(int severity, bool expected)
    {
        // Test-only policy values are not defaults or approved production thresholds.
        var analyzer = new Analyzer { HateSeverity = severity };
        var moderator = new ContentSafetyImageModerator(new Images(), analyzer,
            Options.Create(TestPolicy()));
        (await moderator.ReviewAsync("staging/asset", "image/png", 12, default)).Safe.Should().Be(expected);
        analyzer.Calls.Should().Be(1);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("invalid")]
    public async Task UnexpectedCategoriesFailClosed(string variant)
    {
        var analyzer = new Analyzer { Variant = variant };
        var moderator = new ContentSafetyImageModerator(new Images(), analyzer,
            Options.Create(TestPolicy()));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            moderator.ReviewAsync("staging/asset", "image/png", 12, default));
    }

    [Fact]
    public async Task AzureAdapterSendsS3BytesAsBase64AndParsesAllCategories()
    {
        var handler = new Handler(HttpStatusCode.OK, """
            {"categoriesAnalysis":[
              {"category":"Hate","severity":0},{"category":"SelfHarm","severity":2},
              {"category":"Sexual","severity":0},{"category":"Violence","severity":0}]}
            """);
        var adapter = new AzureContentSafetyAnalyzer(new HttpClient(handler),
            Options.Create(new ContentSafetyOptions
            {
                Endpoint = "https://contentsafety.test.example/",
                SubscriptionKey = "test-only-key"
            }));
        var result = await adapter.AnalyzeAsync(Encoding.ASCII.GetBytes("image-data"), default);
        result.Should().Contain("SelfHarm", 2).And.HaveCount(4);
        handler.Calls.Should().Be(1);
        handler.Address.Should().Be("https://contentsafety.test.example/contentsafety/image:analyze?api-version=2024-09-01");
        handler.Body.Should().Contain("\"content\":\"aW1hZ2UtZGF0YQ==\"");
        handler.Body.Should().Contain("\"outputType\":\"FourSeverityLevels\"");
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "{}")]
    [InlineData(HttpStatusCode.OK, "{}")]
    [InlineData(HttpStatusCode.OK, "{\"categoriesAnalysis\":[]}")]
    public async Task ProviderFailuresOrUnexpectedBodiesNeverProduceAnAnalysis(
        HttpStatusCode status, string body)
    {
        var adapter = new AzureContentSafetyAnalyzer(new HttpClient(new Handler(status, body)),
            Options.Create(new ContentSafetyOptions
            {
                Endpoint = "https://contentsafety.test.example/",
                SubscriptionKey = "test-only-key"
            }));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adapter.AnalyzeAsync([1, 2, 3], default));
    }

    [Theory]
    [InlineData("http://contentsafety.test.example/")]
    [InlineData("https://contentsafety.test.example/path")]
    public async Task AzureAdapterRejectsUntrustedEndpointBeforeNetworkAccess(string endpoint)
    {
        var handler = new Handler(HttpStatusCode.OK, "{}");
        var adapter = new AzureContentSafetyAnalyzer(new HttpClient(handler),
            Options.Create(new ContentSafetyOptions
            {
                Endpoint = endpoint,
                SubscriptionKey = "test-only-key"
            }));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adapter.AnalyzeAsync([1], default));
        handler.Calls.Should().Be(0);
    }

    private static ContentSafetyOptions TestPolicy() => new()
    {
        AllowControlledTestPolicy = true,
        MaxHateSeverity = 0,
        MaxSelfHarmSeverity = 0,
        MaxSexualSeverity = 0,
        MaxViolenceSeverity = 0
    };

    private sealed class Images : IMediaImageSource
    {
        public int Reads;
        public Task<byte[]> ReadForModerationAsync(string objectKey, string mime, long size, CancellationToken ct)
        {
            Reads++;
            return Task.FromResult(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0, 0, 0, 0 });
        }
    }

    private sealed class Analyzer : IContentSafetyAnalyzer
    {
        public int Calls;
        public int HateSeverity;
        public string? Variant;
        public Task<IReadOnlyDictionary<string, int>> AnalyzeAsync(byte[] bytes, CancellationToken ct)
        {
            Calls++;
            var result = new Dictionary<string, int>
            {
                ["Hate"] = HateSeverity,
                ["SelfHarm"] = 0,
                ["Sexual"] = 0,
                ["Violence"] = 0
            };
            if (Variant == "missing") result.Remove("Violence");
            if (Variant == "extra") result["Unexpected"] = 0;
            if (Variant == "invalid") result["Hate"] = -1;
            return Task.FromResult<IReadOnlyDictionary<string, int>>(result);
        }
    }

    private sealed class Handler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int Calls;
        public string? Address;
        public string? Body;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            Address = request.RequestUri?.ToString();
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            request.Headers.Contains("Ocp-Apim-Subscription-Key").Should().BeTrue();
            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        }
    }
}
