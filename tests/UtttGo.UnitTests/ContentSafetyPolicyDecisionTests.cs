using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using UTTTGO.Api.Modules.Media.Application;
using UTTTGO.Api.Modules.Media.Infrastructure;
using Xunit;

namespace UtttGo.UnitTests;

// All responses and bytes are synthetic; proposed thresholds exist only in this fixture.
public sealed class ContentSafetyPolicyDecisionTests
{
    private const string Id = "507f1f77bcf86cd799439011";
    private const string Owner = "test-owner";
    private static readonly string[] Categories = ["Hate", "SelfHarm", "Sexual", "Violence"];

    [Theory]
    [InlineData("Hate", 0, MediaStatus.Approved)]
    [InlineData("Hate", 2, MediaStatus.Rejected)]
    [InlineData("Hate", 4, MediaStatus.Rejected)]
    [InlineData("Hate", 6, MediaStatus.Rejected)]
    [InlineData("SelfHarm", 0, MediaStatus.Approved)]
    [InlineData("SelfHarm", 2, MediaStatus.Rejected)]
    [InlineData("SelfHarm", 4, MediaStatus.Rejected)]
    [InlineData("SelfHarm", 6, MediaStatus.Rejected)]
    [InlineData("Sexual", 0, MediaStatus.Approved)]
    [InlineData("Sexual", 2, MediaStatus.Rejected)]
    [InlineData("Sexual", 4, MediaStatus.Rejected)]
    [InlineData("Sexual", 6, MediaStatus.Rejected)]
    [InlineData("Violence", 0, MediaStatus.Approved)]
    [InlineData("Violence", 2, MediaStatus.Rejected)]
    [InlineData("Violence", 4, MediaStatus.Rejected)]
    [InlineData("Violence", 6, MediaStatus.Rejected)]
    public async Task ProposedSafeOnlyThresholdClassifiesEveryCategoryAndBoundary(
        string category, int severity, string expected)
    {
        var response = JsonSerializer.Serialize(new
        {
            categoriesAnalysis = Categories.Select(name => new
            {
                category = name,
                severity = name == category ? severity : 0
            })
        });
        var scenario = Create(_ => Task.FromResult(JsonResponse(response)));
        var result = await scenario.Service.ConfirmAsync(Id, Owner, default);
        result!.Status.Should().Be(expected);
        scenario.Repository.Value!.Status.Should().Be(expected);
        scenario.Store.Deletes.Should().Be(expected == MediaStatus.Rejected ? 1 : 0);
        (await scenario.Service.GetApprovedReadUrlAsync(Id, default) is not null)
            .Should().Be(expected == MediaStatus.Approved);
        (await scenario.Service.ConfirmAsync(Id, Owner, default))!.Status.Should().Be(expected);
        scenario.Handler.Calls.Should().Be(1);
        scenario.Store.Deletes.Should().Be(expected == MediaStatus.Rejected ? 1 : 0);
    }

    [Theory]
    [InlineData("Hate", null)]
    [InlineData("SelfHarm", null)]
    [InlineData("Sexual", null)]
    [InlineData("Violence", null)]
    [InlineData("Hate", -1)]
    [InlineData("Hate", 1)]
    [InlineData("SelfHarm", 3)]
    [InlineData("Sexual", 5)]
    [InlineData("Violence", 7)]
    public async Task MissingOrUnrecognizedCategorySeverityFailsClosed(string category, int? severity)
    {
        var entries = Categories.Where(name => name != category)
            .Select(name => new { category = name, severity = 0 }).ToArray();
        var json = JsonSerializer.Serialize(new { categoriesAnalysis = entries });
        if (severity is not null)
            json = JsonSerializer.Serialize(new
            {
                categoriesAnalysis = Categories.Select(name => new
                {
                    category = name,
                    severity = name == category ? severity.Value : 0
                })
            });
        var scenario = Create(_ => Task.FromResult(JsonResponse(json)));
        await AssertProcessingFailed(scenario);
    }

    [Theory]
    [InlineData("""{"categoriesAnalysis":[{"category":"Hate","severity":0},{"category":"Hate","severity":0},{"category":"Sexual","severity":0},{"category":"Violence","severity":0}]}""")]
    [InlineData("""{"categoriesAnalysis":[{"category":"Hate","severity":0},{"category":"SelfHarm","severity":0},{"category":"Sexual","severity":0},{"category":"Other","severity":0}]}""")]
    [InlineData("""{"categoriesAnalysis":[{"category":"Hate","severity":0},{"category":"SelfHarm","severity":0},{"category":"Sexual","severity":0},{"category":"Violence"}]}""")]
    [InlineData("""{"categoriesAnalysis":[{"category":"Hate","severity":0},{"category":"SelfHarm","severity":0},{"category":"Sexual","severity":0},{"category":"Violence","severity":"0"}]}""")]
    [InlineData("""{"categoriesAnalysis":null}""")]
    [InlineData("""not-json""")]
    public async Task UnexpectedAzureResponseNeverPublishes(string json)
    {
        var scenario = Create(_ => Task.FromResult(JsonResponse(json)));
        await AssertProcessingFailed(scenario);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task ProviderHttpErrorsNeverCountAsModerationRejection(HttpStatusCode status)
    {
        var scenario = Create(_ => Task.FromResult(new HttpResponseMessage(status)));
        await AssertProcessingFailed(scenario, status is HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError ? 3 : 1);
        scenario.Store.Deletes.Should().Be(0);
    }

    [Fact]
    public async Task ProviderTimeoutMovesToProcessingFailedAndDoesNotDeleteImage()
    {
        var scenario = Create(_ => Task.FromException<HttpResponseMessage>(
            new TaskCanceledException("Simulated provider timeout")));
        await AssertProcessingFailed(scenario, 3);
        scenario.Store.Deletes.Should().Be(0);
    }

    [Fact]
    public async Task TransientProviderErrorsRecoverWithinThreeAttemptsWithoutSecondClaim()
    {
        var attempts = 0;
        var safeResponse = JsonSerializer.Serialize(new
        {
            categoriesAnalysis = Categories.Select(name => new { category = name, severity = 0 })
        });
        var scenario = Create(_ =>
        {
            attempts++;
            return Task.FromResult(attempts < 3
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : JsonResponse(safeResponse));
        });
        (await scenario.Service.ConfirmAsync(Id, Owner, default))!.Status.Should().Be(MediaStatus.Approved);
        scenario.Handler.Calls.Should().Be(3);
        scenario.Repository.Value!.Version.Should().Be(2);
        (await scenario.Service.ConfirmAsync(Id, Owner, default))!.Status.Should().Be(MediaStatus.Approved);
        scenario.Handler.Calls.Should().Be(3);
    }

    [Fact]
    public async Task DisguisedImageIsRejectedWithoutSendingBytesToAzure()
    {
        var scenario = Create(_ => Task.FromResult(JsonResponse("{}")));
        scenario.Store.WrongFormat = true;
        (await scenario.Service.ConfirmAsync(Id, Owner, default))!.Status.Should().Be(MediaStatus.Rejected);
        scenario.Store.Deletes.Should().Be(1);
        scenario.Handler.Calls.Should().Be(0);
        (await scenario.Service.GetApprovedReadUrlAsync(Id, default)).Should().BeNull();
    }

    [Fact]
    public async Task ExplicitThresholdsWithoutIsolatedTestSwitchCannotApprove()
    {
        var images = new Store();
        var options = Options.Create(new ContentSafetyOptions
        {
            MaxHateSeverity = 0,
            MaxSelfHarmSeverity = 0,
            MaxSexualSeverity = 0,
            MaxViolenceSeverity = 0
        });
        var moderator = new ContentSafetyImageModerator(images,
            new AzureContentSafetyAnalyzer(new HttpClient(new Handler(_ =>
                Task.FromResult(JsonResponse("{}")))), options), options);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            moderator.IsSafeAsync("staging/asset", "image/png", 12, default));
        images.Reads.Should().Be(0);
    }

    [Fact]
    public async Task FailedS3ReadAfterHeadKeepsAssetUnpublished()
    {
        var scenario = Create(_ => Task.FromResult(JsonResponse("{}")));
        scenario.Store.FailRead = true;
        (await scenario.Service.ConfirmAsync(Id, Owner, default))!.Status
            .Should().Be(MediaStatus.ProcessingFailed);
        scenario.Handler.Calls.Should().Be(0);
        scenario.Store.Deletes.Should().Be(0);
        (await scenario.Service.GetApprovedReadUrlAsync(Id, default)).Should().BeNull();
    }

    [Fact]
    public async Task FailedDeletionOfUnsafeImageCannotBecomeRejectedOrApproved()
    {
        var unsafeResponse = JsonSerializer.Serialize(new
        {
            categoriesAnalysis = Categories.Select(name =>
                new { category = name, severity = name == "Violence" ? 2 : 0 })
        });
        var scenario = Create(_ => Task.FromResult(JsonResponse(unsafeResponse)));
        scenario.Store.FailDelete = true;
        (await scenario.Service.ConfirmAsync(Id, Owner, default))!.Status
            .Should().Be(MediaStatus.ProcessingFailed);
        scenario.Store.Deletes.Should().Be(1);
        (await scenario.Service.GetApprovedReadUrlAsync(Id, default)).Should().BeNull();
    }

    [Fact]
    public async Task PendingModerationIsObservableUntilProviderCompletes()
    {
        var waiting = new TaskCompletionSource<HttpResponseMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var scenario = Create(_ => waiting.Task);
        var confirmation = scenario.Service.ConfirmAsync(Id, Owner, default);
        scenario.Repository.Value!.Status.Should().Be(MediaStatus.PendingModeration);
        (await scenario.Service.GetApprovedReadUrlAsync(Id, default)).Should().BeNull();
        confirmation.IsCompleted.Should().BeFalse();
        waiting.SetResult(JsonResponse(JsonSerializer.Serialize(new
        {
            categoriesAnalysis = Categories.Select(name => new { category = name, severity = 0 })
        })));
        (await confirmation)!.Status.Should().Be(MediaStatus.Approved);
    }

    private static async Task AssertProcessingFailed(Scenario scenario, int expectedAttempts = 1)
    {
        (await scenario.Service.ConfirmAsync(Id, Owner, default))!.Status
            .Should().Be(MediaStatus.ProcessingFailed);
        scenario.Repository.Value!.Status.Should().Be(MediaStatus.ProcessingFailed);
        (await scenario.Service.GetApprovedReadUrlAsync(Id, default)).Should().BeNull();
        scenario.Store.SignGetCalls.Should().Be(0);
        (await scenario.Service.ConfirmAsync(Id, Owner, default))!.Status
            .Should().Be(MediaStatus.ProcessingFailed);
        scenario.Handler.Calls.Should().Be(expectedAttempts);
    }

    private static Scenario Create(Func<CancellationToken, Task<HttpResponseMessage>> reply)
    {
        var repository = new Repository
        {
            Value = new MediaRecord(Id, Owner, $"staging/{Owner}/{Id}", "image/png",
                12, MediaStatus.PendingUpload, DateTime.UtcNow)
        };
        var store = new Store();
        var handler = new Handler(reply);
        var options = Options.Create(new ContentSafetyOptions
        {
            Endpoint = "https://content-safety.test.example/",
            SubscriptionKey = "fake-key-not-a-secret",
            AllowControlledTestPolicy = true,
            MaxHateSeverity = 0,
            MaxSelfHarmSeverity = 0,
            MaxSexualSeverity = 0,
            MaxViolenceSeverity = 0
        });
        var analyzer = new AzureContentSafetyAnalyzer(new HttpClient(handler), options);
        var moderator = new ContentSafetyImageModerator(store, analyzer, options);
        return new Scenario(new MediaService(repository, store, moderator), repository, store, handler);
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed record Scenario(MediaService Service, Repository Repository, Store Store, Handler Handler);

    private sealed class Handler(Func<CancellationToken, Task<HttpResponseMessage>> reply) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            request.RequestUri!.AbsolutePath.Should().Be("/contentsafety/image:analyze");
            return reply(ct);
        }
    }

    private sealed class Repository : IMediaRepository
    {
        public MediaRecord? Value;
        public Task AddAsync(MediaRecord record, CancellationToken ct) => Task.CompletedTask;
        public Task<MediaRecord?> FindAsync(string id, CancellationToken ct) =>
            Task.FromResult(Value?.Id == id ? Value : null);
        public Task<bool> TransitionAsync(string id, string owner, long version, string from, string to,
            CancellationToken ct)
        {
            if (Value is null || Value.Id != id || Value.OwnerUserId != owner ||
                Value.Version != version || Value.Status != from) return Task.FromResult(false);
            Value = Value with { Status = to, Version = version + 1 };
            return Task.FromResult(true);
        }
    }

    private sealed class Store : IMediaObjectStore, IMediaImageSource
    {
        public int Deletes;
        public int SignGetCalls;
        public bool FailRead;
        public bool FailDelete;
        public bool WrongFormat;
        public int Reads;
        public Task<byte[]> ReadForModerationAsync(string key, string mime, long size, CancellationToken ct)
        {
            Reads++;
            if (FailRead) throw new InvalidOperationException("Simulated S3 read failure");
            return Task.FromResult(WrongFormat ? new byte[12] :
                new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0, 0, 0, 0 });
        }
        public Task<(long Length, string Mime)?> HeadAsync(string key, CancellationToken ct) =>
            Task.FromResult<(long, string)?>((12, "image/png"));
        public Task DeleteAsync(string key, CancellationToken ct)
        {
            Deletes++;
            if (FailDelete) throw new InvalidOperationException("Simulated S3 delete failure");
            return Task.CompletedTask;
        }
        public Task<MediaReadUrl> SignGetAsync(string key, CancellationToken ct)
        {
            SignGetCalls++;
            return Task.FromResult(new MediaReadUrl("https://test.invalid/signed", DateTime.UtcNow.AddMinutes(4)));
        }
        public Task<(string Url, DateTime ExpiresAt, IReadOnlyDictionary<string, string> Headers)>
            SignPutAsync(string key, string mime, CancellationToken ct) =>
            Task.FromResult<(string, DateTime, IReadOnlyDictionary<string, string>)>(
                ("https://test.invalid/upload", DateTime.UtcNow.AddMinutes(4),
                    new Dictionary<string, string> { ["Content-Type"] = mime }));
    }
}
