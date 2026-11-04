using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UTTTGO.Api.Modules.Sellers.Application;
using Xunit;

namespace UtttGo.SecurityTests;

public sealed class SellerProfileSecurityTests
{
    private const string Id = "507f1f77bcf86cd799439011";

    [Fact]
    public async Task PublicProfileDoesNotRequireOrEchoBearerAndCannotLeakPrivateFields()
    {
        using var factory = Factory(new(Id, "Campus Snacks", "Active"));
        using var client = factory.CreateClient();
        using var anonymous = await client.GetAsync($"/api/v1/sellers/{Id}");
        anonymous.StatusCode.Should().Be(HttpStatusCode.OK);
        using var withJwt = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/sellers/{Id}");
        withJwt.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "private-test-token");
        using var authenticated = await client.SendAsync(withJwt);
        authenticated.StatusCode.Should().Be(HttpStatusCode.OK);
        var text = await authenticated.Content.ReadAsStringAsync();
        text.Should().Be(await anonymous.Content.ReadAsStringAsync());
        text.Should().NotContain("private-test-token").And.NotContain("email")
            .And.NotContain("phone").And.NotContain("supabaseUserId")
            .And.NotContain("accountStatus").And.NotContain("products");
        using var json = JsonDocument.Parse(text);
        json.RootElement.EnumerateObject().Select(field => field.Name).Should().BeEquivalentTo(
            ["sellerId", "displayName", "ratingAverage", "ratingCount", "isActive"]);
    }

    [Fact]
    public async Task InactiveSellerRemainsPublicButExplicitlyNotActive()
    {
        using var factory = Factory(new(Id, "Campus Snacks", "Inactive"));
        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/api/v1/sellers/{Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("isActive").GetBoolean().Should().BeFalse();
        json.RootElement.TryGetProperty("acceptingOrders", out _).Should().BeFalse();
        // No Orders module is implemented: this does not prove an order is blocked.
    }

    [Theory]
    [InlineData("../other")]
    [InlineData("507f1f77bcf86cd79943901g")]
    [InlineData("not-a-seller")]
    public async Task MalformedIdIsRejectedBeforeRepository(string id)
    {
        var store = new Store(new(Id, "Campus Snacks", "Active"));
        using var factory = Factory(store: store);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/api/v1/sellers/{Uri.EscapeDataString(id)}");
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
        store.Calls.Should().Be(0);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("Campus Snacks");
    }

    [Fact]
    public async Task QueryCannotChangeSellerIdentity()
    {
        var store = new Store(new(Id, "Campus Snacks", "Active"));
        using var factory = Factory(store: store);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/api/v1/sellers/{Id}?sellerId=other&email=private");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        store.LookedUpId.Should().Be(Id);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("private");
    }

    [Fact]
    public async Task MissingSellerNeverExposesAnotherSeller()
    {
        using var factory = Factory(store: new Store(null));
        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/api/v1/sellers/{Id}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("Campus Snacks");
    }

    private static WebApplicationFactory<Program> Factory(SellerProfileSnapshot? seller = null, Store? store = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPublicSellerRateLimitGate>();
            services.RemoveAll<ISellerProfileRepository>();
            services.RemoveAll<ISellerRatingsReader>();
            services.AddSingleton<IPublicSellerRateLimitGate>(new Allowed());
            services.AddSingleton<ISellerProfileRepository>(store ?? new Store(seller));
            services.AddSingleton<ISellerRatingsReader>(new NoReviews());
        }));

    private sealed class Allowed : IPublicSellerRateLimitGate
    {
        public Task<bool> IsAllowedAsync(string sellerId, CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class Store(SellerProfileSnapshot? profile) : ISellerProfileRepository
    {
        public int Calls { get; private set; }
        public string? LookedUpId { get; private set; }
        public Task<SellerProfileSnapshot?> FindByIdAsync(string sellerId, CancellationToken cancellationToken)
        {
            Calls++;
            LookedUpId = sellerId;
            return Task.FromResult(profile);
        }
    }

    private sealed class NoReviews : ISellerRatingsReader
    {
        public Task<SellerRatingsSnapshot?> GetForSellerAsync(string sellerId,
            CancellationToken cancellationToken) => Task.FromResult<SellerRatingsSnapshot?>(null);
    }
}
