using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace UtttGo.IntegrationTests;

public sealed class OwnProfileEndpointTests
{
    [Fact]
    public async Task OwnProfileReturnsExactPrivateContractAndDiffersFromAuthSession()
    {
        using var factory = SessionEndpointTests.Factory();
        using var response = await Get(factory);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var text = await response.Content.ReadAsStringAsync();
        using var body = JsonDocument.Parse(text);
        body.RootElement.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
            ["id", "displayName", "email", "phone", "avatarUrl", "roles"]);
        body.RootElement.GetProperty("id").GetString().Should().Be("local-id");
        body.RootElement.GetProperty("phone").GetString().Should().Be("1234567890");
        body.RootElement.GetProperty("avatarUrl").ValueKind.Should().Be(JsonValueKind.Null);
        text.Should().NotContain("accountStatus").And.NotContain("test-jwt").And.NotContain("supabaseUserId");
        using var session = await SessionEndpointTests.Get(factory);
        (await session.Content.ReadAsStringAsync()).Should().Contain("accountStatus")
            .And.NotContain("phone");
    }

    [Fact]
    public async Task OnlyApprovedOwnedMediaCanYieldTemporaryAvatarUrl()
    {
        using var factory = SessionEndpointTests.Factory(avatarAssetId: "approved-owned",
            avatarUrl: "https://private.example/short-lived-sas");
        using var response = await Get(factory);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("avatarUrl").GetString().Should()
            .Be("https://private.example/short-lived-sas");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("unapproved")]
    public async Task MissingOrUnapprovedAvatarYieldsNull(string? assetId)
    {
        using var factory = SessionEndpointTests.Factory(avatarAssetId: assetId,
            avatarUrl: "https://private.example/short-lived-sas");
        using var response = await Get(factory);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("avatarUrl").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task WithoutMediaAdapterEvenStoredAssetYieldsNull()
    {
        using var factory = SessionEndpointTests.Factory(avatarAssetId: "approved-owned");
        using var response = await Get(factory);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("avatarUrl").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task MissingProfileIsGeneric401NotProposedExcel404()
    {
        using var factory = SessionEndpointTests.Factory(missingProfile: true);
        using var response = await Get(factory);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().Contain("UNAUTHORIZED")
            .And.NotContain("PROFILE_NOT_FOUND");
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
    public async Task CriticalAuthFailureReturns503WithoutDetails(string failure)
    {
        using var factory = SessionEndpointTests.Factory(failure: failure);
        using var response = await Get(factory);
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).Should().Contain("correlationId")
            .And.NotContain("internal-secret").And.NotContain("test-jwt");
    }

    internal static async Task<HttpResponseMessage> Get(WebApplicationFactory<Program> factory)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-jwt");
        return await client.GetAsync("/api/v1/users/me");
    }
}
