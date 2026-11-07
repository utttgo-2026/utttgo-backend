using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UTTTGO.Api.Modules.Admin.Application;
using UTTTGO.Api.Modules.Identity.Application;
using Xunit;

namespace UtttGo.IntegrationTests;

public sealed class AdminUsersEndpointTests
{
    [Fact]
    public async Task AdminGetsFilteredPageWithOnlyApprovedFields()
    {
        using var factory = Factory();
        using var response = await Get(factory, "?search=ANA&role=Buyer&page=1&pageSize=1");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().NotContain("email").And.NotContain("test-jwt").And.NotContain("createdAt")
            .And.NotContain("phone").And.NotContain("supabaseUserId");
        using var body = JsonDocument.Parse(json);
        body.RootElement.GetProperty("total").GetInt64().Should().Be(2);
        body.RootElement.GetProperty("page").GetInt32().Should().Be(1);
        body.RootElement.GetProperty("pageSize").GetInt32().Should().Be(1);
        var item = body.RootElement.GetProperty("items")[0];
        item.EnumerateObject().Select(property => property.Name).Should()
            .BeEquivalentTo(["id", "displayName", "roles", "accountStatus"]);
        item.GetProperty("id").GetString().Should().Be("a");
    }

    [Theory]
    [InlineData("?page=1&pageSize=2&sortBy=createdAtAsc", "a")]
    [InlineData("?page=1&pageSize=2&sortBy=createdAtDesc", "c")]
    [InlineData("?page=1&pageSize=2", "c")]
    public async Task SortAndDefaultUseTimestampThenId(string query, string first)
    {
        using var factory = Factory();
        using var response = await Get(factory, query);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("items")[0].GetProperty("id").GetString().Should().Be(first);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?page=1")]
    [InlineData("?page=0&pageSize=1")]
    [InlineData("?page=1&pageSize=51")]
    [InlineData("?page=1&pageSize=1&sortBy=email")]
    [InlineData("?page=1&pageSize=1&role=Owner")]
    [InlineData("?page=1&pageSize=1&accountStatus=Deleted")]
    [InlineData("?page=1&page=2&pageSize=1")]
    [InlineData("?page=1&pageSize=1&email=a")]
    public async Task InvalidQueryReturnsSafe400(string query)
    {
        using var factory = Factory();
        using var response = await Get(factory, query);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("correlationId")
            .And.NotContain("test-jwt");
    }

    [Fact]
    public async Task DefaultHostDoesNotOfferSuccessfulAdminList()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var response = await Get(factory, "?page=1&pageSize=1");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("redis", HttpStatusCode.ServiceUnavailable)]
    [InlineData("reader", HttpStatusCode.ServiceUnavailable)]
    [InlineData("audit", HttpStatusCode.ServiceUnavailable)]
    [InlineData("limit", HttpStatusCode.TooManyRequests)]
    public async Task DependenciesFailClosed(string failure, HttpStatusCode expected)
    {
        using var factory = Factory(failure: failure);
        using var response = await Get(factory, "?page=1&pageSize=1");
        response.StatusCode.Should().Be(expected);
        (await response.Content.ReadAsStringAsync()).Should().Contain("correlationId")
            .And.NotContain("test-jwt").And.NotContain("private@uttt.edu.mx")
            .And.NotContain("internal-secret").And.NotContain("items");
    }

    internal static async Task<HttpResponseMessage> Get(WebApplicationFactory<Program> factory, string query,
        string? token = "test-jwt")
    {
        using var client = factory.CreateClient();
        if (token is not null) client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return await client.GetAsync("/api/v1/admin/users" + query);
    }

    internal static WebApplicationFactory<Program> Factory(string role = "Admin", string status = "Active",
        string? failure = null, bool confirmed = true, bool gateAllowed = true) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IValidatedJwtSubjectReader>();
            services.RemoveAll<ILoginJwtGate>();
            services.RemoveAll<ISupabaseSessionIdentity>();
            services.RemoveAll<ILoginProfileRepository>();
            services.RemoveAll<IAdminUserListReader>();
            services.RemoveAll<IAdminUsersAudit>();
            services.RemoveAll<IAdminUsersRateLimitGate>();
            services.AddSingleton<IValidatedJwtSubjectReader>(new Subject());
            services.AddSingleton<ILoginJwtGate>(new Jwt(gateAllowed));
            services.AddSingleton<ISupabaseSessionIdentity>(new Identity(confirmed));
            services.AddSingleton<ILoginProfileRepository>(new Profile(role, status));
            services.AddSingleton<IAdminUserListReader>(new Reader(failure));
            services.AddSingleton<IAdminUsersAudit>(new Audit(failure));
            services.AddSingleton<IAdminUsersRateLimitGate>(new Rate(failure));
        }));

    private sealed class Subject : IValidatedJwtSubjectReader
    {
        public Task<string?> ReadValidatedSubjectAsync(string token, CancellationToken cancellationToken) =>
            Task.FromResult<string?>("sub-1");
    }

    private sealed class Jwt(bool allowed) : ILoginJwtGate
    {
        public Task<bool> IsValidAndNotRevokedAsync(string token, string subject, CancellationToken cancellationToken) =>
            Task.FromResult(allowed);
    }

    private sealed class Identity(bool confirmed) : ISupabaseSessionIdentity
    {
        public Task<ConfirmedSessionIdentity?> FindBySubjectAsync(string subject, CancellationToken cancellationToken) =>
            Task.FromResult<ConfirmedSessionIdentity?>(new("sub-1", "private@uttt.edu.mx", confirmed));
    }

    private sealed class Profile(string role, string status) : ILoginProfileRepository
    {
        public Task<LocalLoginProfile?> FindBySubjectAsync(string subject, CancellationToken cancellationToken) =>
            Task.FromResult<LocalLoginProfile?>(new("actor", "sub-1", "private@uttt.edu.mx",
                status, [role], "Actor"));
    }

    private sealed class Reader(string? failure) : IAdminUserListReader
    {
        public Task<AdminUserPage> ListAsync(AdminUserListQuery query, CancellationToken cancellationToken)
        {
            if (failure == "reader") throw new InvalidOperationException("internal-secret");
            var date = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
            return Task.FromResult(AdminUserListSelection.Select(
            [
                new("b", "Ana", ["Buyer"], "Active", date),
                new("a", "ana Sol", ["Buyer", "Seller"], "Suspended", date),
                new("c", "Beto", ["Admin"], "PendingEmailVerification", date.AddDays(1))
            ], query));
        }
    }

    private sealed class Audit(string? failure) : IAdminUsersAudit
    {
        public Task RecordAsync(string actorId, string correlationId, string result,
            CancellationToken cancellationToken) => failure == "audit"
            ? throw new InvalidOperationException("internal-secret") : Task.CompletedTask;
    }

    private sealed class Rate(string? failure) : IAdminUsersRateLimitGate
    {
        public Task<bool> IsAllowedAsync(string actorId, CancellationToken cancellationToken) =>
            failure == "redis" ? throw new InvalidOperationException("internal-secret") :
            Task.FromResult(failure != "limit");
    }
}
