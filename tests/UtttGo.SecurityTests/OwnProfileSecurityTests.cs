using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Xunit;

namespace UtttGo.SecurityTests;

public sealed class OwnProfileSecurityTests
{
    [Theory]
    [InlineData(null, true, true, "Active", "sub-1", false)]
    [InlineData("sub-1", false, true, "Active", "sub-1", false)]
    [InlineData("sub-1", true, false, "Active", "sub-1", false)]
    [InlineData("sub-1", true, true, "Suspended", "sub-1", false)]
    [InlineData("sub-1", true, true, "PendingEmailVerification", "sub-1", false)]
    [InlineData("sub-1", true, true, "Active", "other", false)]
    [InlineData("sub-1", true, true, "Active", "sub-1", true)]
    public async Task InvalidRevokedUnconfirmedInactiveForeignOrMissingProfileIsGeneric401(
        string? subject, bool gateAllowed, bool confirmed, string status, string localSubject, bool missing)
    {
        using var factory = SessionSecurityTests.Factory(subject, gateAllowed, confirmed, status,
            localSubject, missing);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "secret-token");
        using var response = await client.GetAsync("/api/v1/users/me?userId=other&id=local-id");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().Contain("correlationId")
            .And.NotContain("local-id").And.NotContain("secret-token")
            .And.NotContain("PROFILE_NOT_FOUND");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Basic secret-token")]
    [InlineData("Bearer secret-token extra")]
    public async Task MissingOrMalformedAuthorizationCannotReadProfile(string? header)
    {
        using var factory = SessionSecurityTests.Factory("sub-1", true, true, "Active", "sub-1");
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/me");
        if (header is not null) request.Headers.TryAddWithoutValidation("Authorization", header).Should().BeTrue();
        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SelfProfileNeverLeaksTokenInternalStateOrPrivateOtherUser()
    {
        using var factory = SessionSecurityTests.Factory("sub-1", true, true, "Active", "sub-1");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "secret-token");
        using var response = await client.GetAsync("/api/v1/users/me?userId=other");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("local-id").And.NotContain("secret-token")
            .And.NotContain("accountStatus").And.NotContain("supabaseUserId")
            .And.NotContain("password").And.NotContain("refreshToken");
    }
}
