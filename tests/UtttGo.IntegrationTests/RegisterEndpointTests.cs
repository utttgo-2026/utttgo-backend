using System.Net;
using System.Text;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UTTTGO.Api.Modules.Identity.Application;
using Xunit;

namespace UtttGo.IntegrationTests;

public sealed class RegisterEndpointTests
{
    private const string Route = "/api/v1/auth/register";

    [Fact]
    public async Task NewAndExistingAccountHaveSamePublicAcceptance()
    {
        using var newFactory = CreateFactory(new SupabaseSignUpResult(false, "subject-1", "a@uttt.edu.mx"));
        using var existingFactory = CreateFactory(new SupabaseSignUpResult(true, null, null));

        using var created = await Post(newFactory);
        using var existing = await Post(existingFactory);

        created.StatusCode.Should().Be(HttpStatusCode.Accepted);
        existing.StatusCode.Should().Be(created.StatusCode);
        var body = await created.Content.ReadAsStringAsync();
        (await existing.Content.ReadAsStringAsync()).Should().Be(body);
        body.Should().NotContain("subject-1").And.NotContain("Buyer").And.NotContain("PendingEmailVerification")
            .And.NotContain("userId").And.NotContain("accessToken");
    }

    [Fact]
    public async Task DefaultProductionRegistrationsFailClosed()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var response = await Post(factory);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).Should().Contain("correlationId");
    }

    [Fact]
    public async Task InvalidDomainDoesNotCallProvider()
    {
        using var factory = CreateFactory(new SupabaseSignUpResult(false, "subject-1", "a@uttt.edu.mx"));
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(Route,
            new { email = "a@other.edu.mx", password = "not-stored", displayName = "Alice" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task InjectedRoleIsRejected()
    {
        using var factory = CreateFactory(new SupabaseSignUpResult(false, "subject-1", "a@uttt.edu.mx"));
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(Route,
            new { email = "a@uttt.edu.mx", password = "not-stored", displayName = "Alice", roles = new[] { "Admin" } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task FailedMongoSaveDoesNotReturnAccepted()
    {
        using var factory = CreateFactory(new SupabaseSignUpResult(false, "subject-1", "a@uttt.edu.mx"), failSave: true);
        using var response = await Post(factory);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("subject-1");
    }

    [Fact]
    public async Task UntrustedSupabaseIdentityDoesNotReturnAccepted()
    {
        using var factory = CreateFactory(new SupabaseSignUpResult(false, null, "a@uttt.edu.mx"));
        using var response = await Post(factory);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task RateLimitDenialDoesNotCallSupabase()
    {
        using var factory = CreateFactory(new SupabaseSignUpResult(false, "subject-1", "a@uttt.edu.mx"),
            rateAllowed: false);
        using var response = await Post(factory);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Theory]
    [InlineData("a@sub.uttt.edu.mx")]
    [InlineData("a@uttt.edu.mx.attacker")]
    [InlineData("a@uttt.edu.mxx")]
    [InlineData("a@other@uttt.edu.mx")]
    [InlineData("not-an-email")]
    public async Task SimilarOrInvalidDomainsAreRejected(string email)
    {
        using var factory = CreateFactory(new SupabaseSignUpResult(false, "subject-1", "a@uttt.edu.mx"));
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(Route,
            new { email, password = "private-password", displayName = "Alice" });

        response.IsSuccessStatusCode.Should().BeFalse();
        (await response.Content.ReadAsStringAsync()).Should().Contain("correlationId");
    }

    [Theory]
    [InlineData("{\"email\":\"a@uttt.edu.mx\",\"displayName\":\"Alice\"}")]
    [InlineData("{\"email\":\"a@uttt.edu.mx\",\"password\":\"\",\"displayName\":\"Alice\"}")]
    [InlineData("{\"email\":\"a@uttt.edu.mx\",\"password\":\"   \",\"displayName\":\"Alice\"}")]
    [InlineData("{\"email\":\"a@uttt.edu.mx\",\"password\":42,\"displayName\":\"Alice\"}")]
    [InlineData("{\"email\":[],\"password\":\"private-password\",\"displayName\":\"Alice\"}")]
    [InlineData("{\"email\":\"a@uttt.edu.mx\",\"password\":\"private-password\"}")]
    [InlineData("{\"email\":\"a@uttt.edu.mx\",\"password\":\"private-password\",\"displayName\":\"  \"}")]
    [InlineData("{\"email\":\"a@uttt.edu.mx\",\"password\":\"private-password\",\"displayName\":\"Alice\",\"accountStatus\":\"Active\"}")]
    [InlineData("{\"email\":\"a@uttt.edu.mx\",\"password\":\"private-password\",\"displayName\":\"Alice\",\"roles\":[\"Admin\"]}")]
    [InlineData("{\"email\":\"a@uttt.edu.mx\",\"password\":\"private-password\",\"displayName\":\"Alice\",\"isAdmin\":true}")]
    [InlineData("{\"email\":")]
    [InlineData("null")]
    public async Task InvalidOrPrivilegedJsonReturnsSanitized400(string json)
    {
        using var factory = CreateFactory(new SupabaseSignUpResult(false, "subject-1", "a@uttt.edu.mx"));
        using var client = factory.CreateClient();
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(Route, content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("correlationId").And.NotContain("private-password")
            .And.NotContain("subject-1").And.NotContain("Admin");
    }

    [Fact]
    public async Task ProviderFaultReturnsSanitized503()
    {
        using var factory = CreateFactory(new SupabaseSignUpResult(false, "subject-1", "a@uttt.edu.mx"),
            providerThrows: true);
        using var response = await Post(factory);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("correlationId").And.NotContain("private-password")
            .And.NotContain("InvalidOperationException").And.NotContain("subject-1");
    }

    [Fact]
    public async Task MongoPreflightFaultReturns503()
    {
        using var factory = CreateFactory(new SupabaseSignUpResult(false, "subject-1", "a@uttt.edu.mx"),
            failPreflight: true);
        using var response = await Post(factory);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task OversizedJsonIsRejectedBeforeRegistration()
    {
        using var factory = CreateFactory(new SupabaseSignUpResult(false, "subject-1", "a@uttt.edu.mx"));
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(Route,
            new { email = "a@uttt.edu.mx", password = "private-password", displayName = new string('a', 9 * 1024) });

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        (await response.Content.ReadAsStringAsync()).Should().Contain("REQUEST_TOO_LARGE")
            .And.Contain("correlationId");
    }

    [Fact]
    public async Task OversizedChunkedJsonWithoutContentLengthIsRejected()
    {
        using var factory = CreateFactory(new SupabaseSignUpResult(false, "subject-1", "a@uttt.edu.mx"));
        using var client = factory.CreateClient();
        var json = "{\"email\":\"a@uttt.edu.mx\",\"password\":\"private-password\",\"displayName\":\"" +
            new string('a', 9 * 1024) + "\"}";
        using var content = new ChunkedJsonContent(json);
        using var response = await client.PostAsync(Route, content);

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }

    private static async Task<HttpResponseMessage> Post(WebApplicationFactory<Program> factory)
    {
        using var client = factory.CreateClient();
        return await client.PostAsJsonAsync(Route,
            new { email = "a@uttt.edu.mx", password = "not-stored", displayName = "Alice" });
    }

    private static WebApplicationFactory<Program> CreateFactory(SupabaseSignUpResult signUp,
        bool failSave = false, bool rateAllowed = true, bool providerThrows = false,
        bool failPreflight = false) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAuthRateLimitGate>();
            services.RemoveAll<ISupabaseRegistration>();
            services.RemoveAll<IUserRegistrationRepository>();
            services.AddSingleton<IAuthRateLimitGate>(new TestRateGate(rateAllowed));
            services.AddSingleton<ISupabaseRegistration>(new FakeSupabase(signUp, providerThrows));
            services.AddSingleton<IUserRegistrationRepository>(new FakeUsers(failSave, failPreflight));
        }));

    private sealed class TestRateGate(bool allowed) : IAuthRateLimitGate
    {
        public Task<bool> IsAllowedAsync(CancellationToken cancellationToken) => Task.FromResult(allowed);
    }

    private sealed class FakeSupabase(SupabaseSignUpResult result, bool throws) : ISupabaseRegistration
    {
        public Task<SupabaseSignUpResult> SignUpAsync(string email, string password,
            CancellationToken cancellationToken) => throws
                ? throw new InvalidOperationException("private-password internal provider failure")
                : Task.FromResult(result);
    }

    private sealed class FakeUsers(bool failSave, bool failPreflight) : IUserRegistrationRepository
    {
        public Task EnsureAvailableAsync(CancellationToken cancellationToken) => failPreflight
            ? throw new ExternalDependencyUnavailableException()
            : Task.CompletedTask;
        public Task SavePendingBuyerAsync(PendingBuyerProfile profile, CancellationToken cancellationToken) => failSave
                ? throw new ExternalDependencyUnavailableException()
                : Task.CompletedTask;
    }

    private sealed class ChunkedJsonContent : HttpContent
    {
        private readonly byte[] bytes;

        public ChunkedJsonContent(string json)
        {
            bytes = Encoding.UTF8.GetBytes(json);
            Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) =>
            stream.WriteAsync(bytes, 0, bytes.Length);
    }
}
