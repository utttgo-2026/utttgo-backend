using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Bson;
using StackExchange.Redis;
using Testcontainers.Redis;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Media.Application;
using Xunit;

namespace UtttGo.SecurityTests;

public sealed class MediaSecurityTests : IAsyncLifetime
{
    private const string Id = "507f1f77bcf86cd799439011";
    private readonly RedisContainer redis = new RedisBuilder("redis:7-alpine").Build();
    private IConnectionMultiplexer connection = null!;

    public async Task InitializeAsync()
    {
        await redis.StartAsync();
        connection = await ConnectionMultiplexer.ConnectAsync(
            $"{redis.Hostname}:{redis.GetMappedPublicPort(6379)}");
    }

    public async Task DisposeAsync()
    {
        await connection.DisposeAsync();
        await redis.DisposeAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidOrRevokedTokenCannotReadOrConfirm(bool revoked)
    {
        var repo = new Repository { Value = Asset() };
        var store = new Store();
        using var factory = Factory(repo, store, new AuthenticatedProfileDouble
        {
            Profile = SellerFixtures.Profile("Active", true, SellerFixtures.SellerId),
            Outcome = AuthenticatedLocalProfileOutcome.Unauthorized
        });
        using var client = factory.CreateClient();
        if (revoked) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "revoked");
        using var response = await client.PostAsJsonAsync("/api/v1/media/confirm", new { mediaAssetId = Id });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        store.HeadCalls.Should().Be(0);
        repo.Value!.Status.Should().Be(MediaStatus.PendingUpload);
    }

    [Fact]
    public async Task OtherOwnersCannotSeeStateOrReachS3()
    {
        var repo = new Repository { Value = Asset() };
        var store = new Store();
        using var factory = Factory(repo, store, Profile("other"));
        using var client = Client(factory);
        using var read = await client.GetAsync($"/api/v1/media/{Id}");
        using var complete = await client.PostAsJsonAsync("/api/v1/media/confirm",
            new { mediaAssetId = Id });
        read.StatusCode.Should().Be(HttpStatusCode.NotFound);
        complete.StatusCode.Should().Be(HttpStatusCode.NotFound);
        store.HeadCalls.Should().Be(0);
        store.SignGetCalls.Should().Be(0);
    }

    [Theory]
    [InlineData(MediaStatus.PendingUpload)]
    [InlineData(MediaStatus.PendingModeration)]
    [InlineData(MediaStatus.Rejected)]
    [InlineData(MediaStatus.ProcessingFailed)]
    public async Task UnapprovedAssetsNeverReturnS3ReadUrls(string status)
    {
        var repo = new Repository { Value = Asset() with { Status = status } };
        var store = new Store();
        using var factory = Factory(repo, store, Profile());
        using var client = Client(factory);
        using var response = await client.GetAsync($"/api/v1/media/{Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("imageUrl").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        store.SignGetCalls.Should().Be(0);
    }

    [Fact]
    public async Task MissingModeratorCannotApproveAndRepeatConfirmationIsStable()
    {
        var repo = new Repository { Value = Asset() };
        var store = new Store { Head = (10, "image/jpeg") };
        using var factory = Factory(repo, store, Profile());
        using var client = Client(factory);
        using var response = await client.PostAsJsonAsync("/api/v1/media/confirm",
            new { mediaAssetId = Id });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain(MediaStatus.ProcessingFailed);
        using var again = await client.PostAsJsonAsync($"/api/v1/media/{Id}/complete", new { });
        (await again.Content.ReadAsStringAsync()).Should().Contain(MediaStatus.ProcessingFailed);
        repo.Value!.Status.Should().NotBe(MediaStatus.Approved);
        store.SignGetCalls.Should().Be(0);
    }

    private static AuthenticatedProfileDouble Profile(string? owner = null) =>
        new()
        {
            Profile = SellerFixtures.Profile("Active", true, SellerFixtures.SellerId)
            with
            { Id = owner ?? SellerFixtures.UserId }
        };

    private WebApplicationFactory<Program> Factory(Repository repository, Store store,
        AuthenticatedProfileDouble profile) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IMediaRepository>();
                services.RemoveAll<IMediaObjectStore>();
                services.RemoveAll<IAuthenticatedLocalProfileReader>();
                services.RemoveAll<IConnectionMultiplexer>();
                services.AddSingleton<IMediaRepository>(repository);
                services.AddSingleton<IMediaObjectStore>(store);
                services.AddSingleton<IAuthenticatedLocalProfileReader>(profile);
                services.AddSingleton(connection);
            }));

    private static HttpClient Client(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "local-test-token");
        return client;
    }

    private static MediaRecord Asset() =>
        new(Id, SellerFixtures.UserId, "staging/local/" + Id, "image/jpeg", 10,
            MediaStatus.PendingUpload, DateTime.UtcNow);

    private sealed class Repository : IMediaRepository
    {
        public MediaRecord? Value;
        public Task AddAsync(MediaRecord value, CancellationToken ct)
        {
            Value = value;
            return Task.CompletedTask;
        }
        public Task<MediaRecord?> FindAsync(string id, CancellationToken ct) =>
            Task.FromResult(Value?.Id == id ? Value : null);
        public Task<bool> TransitionAsync(string id, string owner, long version, string from, string to,
            CancellationToken ct)
        {
            if (Value is null || Value.Id != id || Value.OwnerUserId != owner ||
                Value.Status != from || Value.Version != version) return Task.FromResult(false);
            Value = Value with { Status = to, Version = version + 1 };
            return Task.FromResult(true);
        }
    }

    private sealed class Store : IMediaObjectStore
    {
        public (long Length, string Mime)? Head;
        public int HeadCalls;
        public int SignGetCalls;
        public Task<(string Url, DateTime ExpiresAt, IReadOnlyDictionary<string, string> Headers)>
            SignPutAsync(string key, string mime, CancellationToken ct) =>
            Task.FromResult<(string, DateTime, IReadOnlyDictionary<string, string>)>(
                ("https://local.invalid/put", DateTime.UtcNow.AddMinutes(4),
                    new Dictionary<string, string> { ["Content-Type"] = mime }));
        public Task<(long Length, string Mime)?> HeadAsync(string key, CancellationToken ct)
        {
            HeadCalls++;
            return Task.FromResult(Head);
        }
        public Task<MediaReadUrl> SignGetAsync(string key, CancellationToken ct)
        {
            SignGetCalls++;
            return Task.FromResult(new MediaReadUrl("https://local.invalid/get", DateTime.UtcNow.AddMinutes(4)));
        }
        public Task DeleteAsync(string key, CancellationToken ct) => Task.CompletedTask;
    }
}
