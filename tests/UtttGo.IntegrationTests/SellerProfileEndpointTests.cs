using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UTTTGO.Api.Modules.Sellers.Application;
using Xunit;

namespace UtttGo.IntegrationTests;

public sealed class SellerProfileEndpointTests
{
    internal const string Id = "507f1f77bcf86cd799439011";
    private static readonly SellerProfileSnapshot Active = new(Id, "Campus Snacks", "Active");

    [Theory]
    [InlineData("Active", true)]
    [InlineData("Inactive", false)]
    public async Task PublicSellerHasExactProjectionWithoutInventedReviews(string status, bool active)
    {
        using var factory = Factory(seller: Active with { Status = status });
        using var response = await Get(factory);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
            ["sellerId", "displayName", "ratingAverage", "ratingCount", "isActive"]);
        body.RootElement.GetProperty("sellerId").GetString().Should().Be(Id);
        body.RootElement.GetProperty("displayName").GetString().Should().Be("Campus Snacks");
        body.RootElement.GetProperty("isActive").GetBoolean().Should().Be(active);
        body.RootElement.GetProperty("ratingAverage").ValueKind.Should().Be(JsonValueKind.Null);
        body.RootElement.GetProperty("ratingCount").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task UppercaseHexIdResolvesCanonicalSeller()
    {
        using var factory = Factory();
        using var response = await Get(factory, Id.ToUpperInvariant());
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("sellerId").GetString().Should().Be(Id);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("507f1f77bcf86cd79943901g")]
    [InlineData("00000000000000000000000000000000")]
    [InlineData("%3Cscript%3E")]
    public async Task InvalidIdIsSanitized400(string id)
    {
        var store = new Store(Active);
        using var factory = Factory(store: store);
        using var response = await Get(factory, id);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("INVALID_SELLER_ID")
            .And.Contain("correlationId").And.NotContain("script");
        store.Calls.Should().Be(0);
    }

    [Fact]
    public async Task MissingSellerReturns404()
    {
        using var factory = Factory(missing: true);
        using var response = await Get(factory);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().Contain("SELLER_NOT_FOUND")
            .And.NotContain("Campus Snacks");
    }

    [Theory]
    [InlineData("Deleted")]
    [InlineData("Suspended")]
    public async Task DeletedOrSuspendedSellerIsNotPublished(string status)
    {
        using var factory = Factory(seller: Active with { Status = status });
        using var response = await Get(factory);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReviewsSuccessOnlyUsesReviewsSuppliedRatings()
    {
        using var factory = Factory(reviews: new Reviews(new(4.5m, 9)));
        using var response = await Get(factory);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("ratingAverage").GetDecimal().Should().Be(4.5m);
        body.RootElement.GetProperty("ratingCount").GetInt64().Should().Be(9);
    }

    [Fact]
    public async Task ReviewsFailureLeavesKnownSellerVisibleWithoutInventingRatings()
    {
        using var factory = Factory(seller: Active with { Status = "Inactive" },
            reviews: new Reviews(null, fail: true));
        using var response = await Get(factory);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("isActive").GetBoolean().Should().BeFalse();
        body.RootElement.GetProperty("ratingAverage").ValueKind.Should().Be(JsonValueKind.Null);
        body.RootElement.GetProperty("ratingCount").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task RateLimitDenialReturns429WithoutReadingSeller()
    {
        var store = new Store(Active);
        using var factory = Factory(store: store, gate: new Gate(allowed: false));
        using var response = await Get(factory);
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        store.Calls.Should().Be(0);
    }

    [Theory]
    [InlineData("gate")]
    [InlineData("seller")]
    public async Task CriticalFailureReturns503WithoutExceptionDetails(string failure)
    {
        using var factory = Factory(gate: new Gate(fail: failure == "gate"),
            store: new Store(Active, fail: failure == "seller"));
        using var response = await Get(factory);
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).Should().Contain("correlationId")
            .And.NotContain("internal-secret");
    }

    [Fact]
    public async Task UnconfiguredApplicationFailsClosedForPublicRead()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var response = await Get(factory);
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    internal static Task<HttpResponseMessage> Get(WebApplicationFactory<Program> factory, string id = Id) =>
        factory.CreateClient().GetAsync($"/api/v1/sellers/{id}");

    internal static WebApplicationFactory<Program> Factory(SellerProfileSnapshot? seller = null,
        bool missing = false, Gate? gate = null, Store? store = null, Reviews? reviews = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPublicSellerRateLimitGate>();
            services.RemoveAll<ISellerProfileRepository>();
            services.RemoveAll<ISellerRatingsReader>();
            services.AddSingleton<IPublicSellerRateLimitGate>(gate ?? new Gate());
            services.AddSingleton<ISellerProfileRepository>(store ?? new Store(missing ? null : seller ?? Active));
            services.AddSingleton<ISellerRatingsReader>(reviews ?? new Reviews(null));
        }));

    internal sealed class Gate(bool allowed = true, bool fail = false) : IPublicSellerRateLimitGate
    {
        public Task<bool> IsAllowedAsync(string sellerId, CancellationToken cancellationToken) =>
            fail ? throw new InvalidOperationException("internal-secret") : Task.FromResult(allowed);
    }

    internal sealed class Store(SellerProfileSnapshot? seller, bool fail = false) : ISellerProfileRepository
    {
        public int Calls { get; private set; }
        public Task<SellerProfileSnapshot?> FindByIdAsync(string sellerId, CancellationToken cancellationToken)
        {
            Calls++;
            return fail ? throw new InvalidOperationException("internal-secret") : Task.FromResult(seller);
        }
    }

    internal sealed class Reviews(SellerRatingsSnapshot? review, bool fail = false) : ISellerRatingsReader
    {
        public Task<SellerRatingsSnapshot?> GetForSellerAsync(string sellerId, CancellationToken cancellationToken) =>
            fail ? throw new InvalidOperationException("internal-secret") : Task.FromResult(review);
    }
}
