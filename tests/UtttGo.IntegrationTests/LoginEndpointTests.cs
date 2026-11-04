using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UTTTGO.Api.Modules.Identity.Application;
using Xunit;

namespace UtttGo.IntegrationTests;

public sealed class LoginEndpointTests
{
    private const string Route = "/api/v1/auth/login";
    private static readonly SupabaseLoginResult Verified =
        new(true, true, "provider-subject", "a@uttt.edu.mx", "test-only-jwt", 3600);
    private static readonly LocalLoginProfile Active =
        new("local-id", "provider-subject", "a@uttt.edu.mx", "Active", ["Seller"]);

    [Fact]
    public async Task ValidConfirmedLoginReturnsTokenAndLocalRoleOnly()
    {
        using var factory = CreateFactory();
        using var response = await Post(factory);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("accessToken").GetString().Should().Be("test-only-jwt");
        body.RootElement.GetProperty("expiresIn").GetInt64().Should().Be(3600);
        body.RootElement.GetProperty("tokenType").GetString().Should().Be("Bearer");
        body.RootElement.GetProperty("user").GetProperty("id").GetString().Should().Be("local-id");
        body.RootElement.GetProperty("user").GetProperty("roles")[0].GetString().Should().Be("Seller");
        body.RootElement.GetProperty("user").GetProperty("accountStatus").GetString().Should().Be("Active");
        body.RootElement.TryGetProperty("refreshToken", out _).Should().BeFalse();
        (await response.Content.ReadAsStringAsync()).Should().NotContain("private-password");
    }

    [Theory]
    [InlineData(false, true, "Active")]
    [InlineData(true, false, "Active")]
    [InlineData(true, true, "PendingEmailVerification")]
    [InlineData(true, true, "Suspended")]
    [InlineData(true, true, "Deleted")]
    public async Task UnauthenticatedOrInactiveAccountsGetGeneric401WithoutToken(
        bool valid, bool confirmed, string status)
    {
        using var factory = CreateFactory(provider: Verified with
        {
            CredentialsValid = valid,
            EmailConfirmed = confirmed
        }, profile: Active with { AccountStatus = status });
        using var response = await Post(factory);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("INVALID_CREDENTIALS").And.Contain("correlationId")
            .And.NotContain("test-only-jwt").And.NotContain("private-password")
            .And.NotContain("PendingEmailVerification").And.NotContain("Suspended");
    }

    [Fact]
    public async Task MissingProfileIsNotCreatedFromProviderJwt()
    {
        using var factory = CreateFactory(missingProfile: true);
        using var response = await Post(factory);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("test-only-jwt");
    }

    [Theory]
    [InlineData("{\"email\":\"a@uttt.edu.mx\"}")]
    [InlineData("{\"email\":\"a@uttt.edu.mx\",\"password\":\"\"}")]
    [InlineData("{\"email\":\"not-email\",\"password\":\"private-password\"}")]
    [InlineData("{\"email\":\"a@uttt.edu.mx\",\"password\":\"private-password\",\"roles\":[\"Admin\"]}")]
    [InlineData("{\"email\":\"a@uttt.edu.mx\",\"password\":\"private-password\",\"accountStatus\":\"Active\"}")]
    [InlineData("{\"email\":")]
    public async Task InvalidOrInjectedBodyReturnsSanitized400(string json)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(Route, content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("correlationId").And.NotContain("private-password")
            .And.NotContain("Admin").And.NotContain("test-only-jwt");
    }

    [Fact]
    public async Task DefaultUnconfiguredProductionDependenciesReturn503()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var response = await Post(factory);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).Should().Contain("correlationId")
            .And.NotContain("private-password");
    }

    [Theory]
    [InlineData("provider")]
    [InlineData("profile")]
    [InlineData("jwt")]
    [InlineData("redis")]
    public async Task FailedCriticalDependencyReturns503WithoutTokenOrException(string failure)
    {
        using var factory = CreateFactory(failure: failure);
        using var response = await Post(factory);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("AUTH_UNAVAILABLE").And.Contain("correlationId")
            .And.NotContain("test-only-jwt").And.NotContain("private-password")
            .And.NotContain("InvalidOperationException");
    }

    [Fact]
    public async Task RateLimitedLoginReturns429WithoutToken()
    {
        using var factory = CreateFactory(rateAllowed: false);
        using var response = await Post(factory);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("test-only-jwt");
    }

    private static async Task<HttpResponseMessage> Post(WebApplicationFactory<Program> factory)
    {
        using var client = factory.CreateClient();
        return await client.PostAsJsonAsync(Route,
            new { email = "a@uttt.edu.mx", password = "private-password" });
    }

    private static WebApplicationFactory<Program> CreateFactory(SupabaseLoginResult? provider = null,
        LocalLoginProfile? profile = null, bool missingProfile = false, bool rateAllowed = true,
        string? failure = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAuthRateLimitGate>();
            services.RemoveAll<ISupabaseLogin>();
            services.RemoveAll<ILoginProfileRepository>();
            services.RemoveAll<ILoginJwtGate>();
            services.AddSingleton<IAuthRateLimitGate>(new TestGate(rateAllowed, failure == "redis"));
            services.AddSingleton<ISupabaseLogin>(new TestProvider(provider ?? Verified, failure == "provider"));
            services.AddSingleton<ILoginProfileRepository>(new TestProfiles(
                missingProfile ? null : profile ?? Active, failure == "profile"));
            services.AddSingleton<ILoginJwtGate>(new TestJwtGate(failure == "jwt"));
        }));

    private sealed class TestGate(bool allowed, bool fail) : IAuthRateLimitGate
    {
        public Task<bool> IsAllowedAsync(CancellationToken cancellationToken) => fail
            ? throw new InvalidOperationException("private-password internal Redis detail")
            : Task.FromResult(allowed);
    }

    private sealed class TestProvider(SupabaseLoginResult response, bool fail) : ISupabaseLogin
    {
        public Task<SupabaseLoginResult> AuthenticateAndCheckConfirmationAsync(string email, string password,
            CancellationToken cancellationToken) => fail
                ? throw new InvalidOperationException("private-password internal provider detail")
                : Task.FromResult(response);
    }

    private sealed class TestProfiles(LocalLoginProfile? profile, bool fail) : ILoginProfileRepository
    {
        public Task<LocalLoginProfile?> FindBySubjectAsync(string subject, CancellationToken cancellationToken) =>
            fail ? throw new InvalidOperationException("private-password internal database detail")
                : Task.FromResult(profile);
    }

    private sealed class TestJwtGate(bool fail) : ILoginJwtGate
    {
        public Task<bool> IsValidAndNotRevokedAsync(string token, string subject,
            CancellationToken cancellationToken) => fail
                ? throw new InvalidOperationException("test-only-jwt internal validation detail")
                : Task.FromResult(true);
    }
}
