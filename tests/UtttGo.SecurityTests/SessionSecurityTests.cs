using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UTTTGO.Api.Modules.Identity.Application;
using Xunit;

namespace UtttGo.SecurityTests;

public sealed class SessionSecurityTests
{
    [Theory]
    [InlineData(null, true, true, "Active", "sub-1")]
    [InlineData("sub-1", false, true, "Active", "sub-1")]
    [InlineData("sub-1", true, false, "Active", "sub-1")]
    [InlineData("sub-1", true, true, "Suspended", "sub-1")]
    [InlineData("sub-1", true, true, "PendingEmailVerification", "sub-1")]
    [InlineData("sub-1", true, true, "Active", "other-sub")]
    public async Task InvalidExpiredRevokedUnconfirmedInactiveOrForeignSessionCannotReadIdentity(
        string? subject, bool notRevoked, bool confirmed, string status, string localSubject)
    {
        using var factory = Factory(subject, notRevoked, confirmed, status, localSubject);
        using var response = await Get(factory);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().Contain("correlationId")
            .And.NotContain("local-id").And.NotContain("secret-token");
    }

    [Fact]
    public async Task QueryIdentityNeverOverridesValidatedSubject()
    {
        using var factory = Factory("sub-1", true, true, "Active", "other-sub");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "secret-token");
        using var response = await client.GetAsync("/api/v1/auth/me?sub=other-sub&id=local-id");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SuccessfulSessionNeverDisclosesTokenOrEditableProfileFields()
    {
        using var factory = Factory("sub-1", true, true, "Active", "sub-1");
        using var response = await Get(factory);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("secret-token")
            .And.NotContain("refreshToken").And.NotContain("avatarUrl")
            .And.NotContain("supabaseUserId");
    }

    private static async Task<HttpResponseMessage> Get(WebApplicationFactory<Program> factory)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "secret-token");
        return await client.GetAsync("/api/v1/auth/me");
    }

    internal static WebApplicationFactory<Program> Factory(string? subject, bool allowed, bool confirmed,
        string status, string localSubject, bool missingProfile = false) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IValidatedJwtSubjectReader>();
            services.RemoveAll<ILoginJwtGate>();
            services.RemoveAll<ISupabaseSessionIdentity>();
            services.RemoveAll<ILoginProfileRepository>();
            services.AddSingleton<IValidatedJwtSubjectReader>(new SubjectDouble(subject));
            services.AddSingleton<ILoginJwtGate>(new GateDouble(allowed));
            services.AddSingleton<ISupabaseSessionIdentity>(new IdentityDouble(confirmed));
            services.AddSingleton<ILoginProfileRepository>(new ProfileDouble(localSubject, status, missingProfile));
        }));

    private sealed class SubjectDouble(string? value) : IValidatedJwtSubjectReader
    {
        public Task<string?> ReadValidatedSubjectAsync(string token, CancellationToken cancellationToken) =>
            Task.FromResult(value);
    }

    private sealed class GateDouble(bool allowed) : ILoginJwtGate
    {
        public Task<bool> IsValidAndNotRevokedAsync(string token, string subject, CancellationToken cancellationToken) =>
            Task.FromResult(allowed);
    }

    private sealed class IdentityDouble(bool confirmed) : ISupabaseSessionIdentity
    {
        public Task<ConfirmedSessionIdentity?> FindBySubjectAsync(string subject, CancellationToken cancellationToken) =>
            Task.FromResult<ConfirmedSessionIdentity?>(new("sub-1", "a@uttt.edu.mx", confirmed));
    }

    private sealed class ProfileDouble(string subject, string status, bool missing) : ILoginProfileRepository
    {
        public Task<LocalLoginProfile?> FindBySubjectAsync(string lookup, CancellationToken cancellationToken) =>
            Task.FromResult<LocalLoginProfile?>(missing ? null : new("local-id", subject, "a@uttt.edu.mx",
                status, ["Buyer"], "Alumna"));
    }
}
