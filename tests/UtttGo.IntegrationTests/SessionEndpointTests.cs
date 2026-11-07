using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UTTTGO.Api.Modules.Identity.Application;
using Xunit;

namespace UtttGo.IntegrationTests;

public sealed class SessionEndpointTests
{
    [Fact]
    public async Task ConfirmedActiveSessionReturnsExactContractWithoutToken()
    {
        using var factory = Factory();
        using var response = await Get(factory);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var text = await response.Content.ReadAsStringAsync();
        text.Should().NotContain("test-jwt").And.NotContain("accessToken")
            .And.NotContain("refreshToken").And.NotContain("avatarUrl");
        using var body = JsonDocument.Parse(text);
        body.RootElement.EnumerateObject().Select(property => property.Name).Should()
            .BeEquivalentTo(["id", "email", "displayName", "roles", "accountStatus"]);
        body.RootElement.GetProperty("id").GetString().Should().Be("local-id");
        body.RootElement.GetProperty("displayName").GetString().Should().Be("Alumna");
        body.RootElement.GetProperty("roles")[0].GetString().Should().Be("Buyer");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Basic test-jwt")]
    [InlineData("Bearer ")]
    [InlineData("Bearer test-jwt extra")]
    public async Task MissingOrMalformedAuthorizationReturns401(string? header)
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        if (header is not null && header.Length > 0)
            request.Headers.TryAddWithoutValidation("Authorization", header).Should().BeTrue();
        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().Contain("correlationId").And.NotContain("test-jwt");
    }

    [Fact]
    public async Task InvalidTokenOnDefaultHostIsUnauthorized()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var response = await Get(factory);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("subject")]
    [InlineData("redis")]
    [InlineData("supabase")]
    [InlineData("mongo")]
    public async Task CriticalFailureReturnsSanitized503(string failure)
    {
        using var factory = Factory(failure: failure);
        using var response = await Get(factory);
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).Should().Contain("correlationId")
            .And.NotContain("test-jwt").And.NotContain("internal-secret");
    }

    [Fact]
    public async Task MissingLocalProfileReturns404WithoutIdentity()
    {
        using var factory = Factory(missingProfile: true);
        using var response = await Get(factory);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().Contain("PROFILE_NOT_FOUND")
            .And.NotContain("local-id");
    }

    internal static async Task<HttpResponseMessage> Get(WebApplicationFactory<Program> factory,
        string token = "test-jwt")
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.GetAsync("/api/v1/auth/me");
    }

    internal static WebApplicationFactory<Program> Factory(string? subject = "sub-1", bool gateAllowed = true,
        bool confirmed = true, string status = "Active", bool missingProfile = false,
        string? profileSubject = null, string? failure = null, string? avatarAssetId = null,
        string? avatarUrl = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IValidatedJwtSubjectReader>();
            services.RemoveAll<ILoginJwtGate>();
            services.RemoveAll<ISupabaseSessionIdentity>();
            services.RemoveAll<ILoginProfileRepository>();
            if (avatarUrl is not null)
            {
                services.RemoveAll<IApprovedAvatarReadAccess>();
                services.AddSingleton<IApprovedAvatarReadAccess>(new ApprovedAvatarDouble(avatarUrl));
            }
            services.AddSingleton<IValidatedJwtSubjectReader>(new SubjectDouble(subject, failure == "subject"));
            services.AddSingleton<ILoginJwtGate>(new GateDouble(gateAllowed, failure == "redis"));
            services.AddSingleton<ISupabaseSessionIdentity>(new IdentityDouble(confirmed, failure == "supabase"));
            services.AddSingleton<ILoginProfileRepository>(new ProfileDouble(missingProfile ? null :
                new LocalLoginProfile("local-id", profileSubject ?? "sub-1", "a@uttt.edu.mx",
                    status, ["Buyer"], "Alumna", "1234567890", avatarAssetId), failure == "mongo"));
        }));

    private sealed class ApprovedAvatarDouble(string url) : IApprovedAvatarReadAccess
    {
        public Task<string?> GetApprovedOwnedTemporaryUrlAsync(string mediaAssetId, string ownerId,
            CancellationToken cancellationToken) => Task.FromResult<string?>(
                mediaAssetId == "approved-owned" && ownerId == "local-id" ? url : null);
    }

    private sealed class SubjectDouble(string? value, bool fail) : IValidatedJwtSubjectReader
    {
        public Task<string?> ReadValidatedSubjectAsync(string token, CancellationToken cancellationToken) =>
            fail ? throw new InvalidOperationException("internal-secret") : Task.FromResult(value);
    }

    private sealed class GateDouble(bool allowed, bool fail) : ILoginJwtGate
    {
        public Task<bool> IsValidAndNotRevokedAsync(string token, string subject, CancellationToken cancellationToken) =>
            fail ? throw new InvalidOperationException("internal-secret") : Task.FromResult(allowed);
    }

    private sealed class IdentityDouble(bool confirmed, bool fail) : ISupabaseSessionIdentity
    {
        public Task<ConfirmedSessionIdentity?> FindBySubjectAsync(string subject, CancellationToken cancellationToken) =>
            fail ? throw new InvalidOperationException("internal-secret") :
                Task.FromResult<ConfirmedSessionIdentity?>(new("sub-1", "a@uttt.edu.mx", confirmed));
    }

    private sealed class ProfileDouble(LocalLoginProfile? profile, bool fail) : ILoginProfileRepository
    {
        public Task<LocalLoginProfile?> FindBySubjectAsync(string subject, CancellationToken cancellationToken) =>
            fail ? throw new InvalidOperationException("internal-secret") : Task.FromResult(profile);
    }
}
