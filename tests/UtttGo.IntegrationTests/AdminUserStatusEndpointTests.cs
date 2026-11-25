using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UTTTGO.Api.Modules.Admin.Application;
using UTTTGO.Api.Modules.Catalog.Application;
using UTTTGO.Api.Modules.Identity.Application;
using Xunit;

namespace UtttGo.IntegrationTests;

public sealed class AdminUserStatusEndpointTests
{
    [Fact]
    public async Task PatchSuspendsExistingUser()
    {
        using var factory = Factory(new Target("target", "Active"));
        using var client = Client(factory);
        using var response = await client.PatchAsJsonAsync("/api/v1/admin/users/target/status",
            new { accountStatus = "Suspended" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"accountStatus\":\"Suspended\"");
    }

    [Fact]
    public async Task SameStatusIsIdempotent()
    {
        using var factory = Factory(new Target("target", "Active"));
        using var response = await Client(factory).PatchAsJsonAsync("/api/v1/admin/users/target/status",
            new { accountStatus = "Active" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("missing", HttpStatusCode.NotFound)]
    [InlineData("target", HttpStatusCode.BadRequest)]
    public async Task InvalidTargetOrBodyUsesContract(string target, HttpStatusCode expected)
    {
        using var factory = Factory(new Target("target", "Active"));
        using var client = Client(factory);
        using var response = expected == HttpStatusCode.BadRequest
            ? await client.PatchAsJsonAsync($"/api/v1/admin/users/{target}/status", new { accountStatus = "Deleted" })
            : await client.PatchAsJsonAsync($"/api/v1/admin/users/{target}/status", new { accountStatus = "Active" });
        response.StatusCode.Should().Be(expected);
        (await response.Content.ReadAsStringAsync()).Should().Contain("correlationId");
    }

    private static HttpClient Client(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        return client;
    }

    private static WebApplicationFactory<Program> Factory(Target target) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISessionService>();
            services.RemoveAll<IAdminUserStatusWriter>();
            services.RemoveAll<IAdminUserStatusRateLimitGate>();
            services.RemoveAll<IAccountSessionRevoker>();
            services.RemoveAll<IDeviceTokenStatusRemover>();
            services.RemoveAll<IAccountStatusCatalog>();
            services.RemoveAll<IAdminUserStatusAudit>();
            services.AddSingleton<ISessionService>(new Session());
            services.AddSingleton<IAdminUserStatusWriter>(new Writer(target));
            services.AddSingleton<IAdminUserStatusRateLimitGate>(new Gate());
            services.AddSingleton<IAccountSessionRevoker>(new Revoker());
            services.AddSingleton<IDeviceTokenStatusRemover>(new Devices());
            services.AddSingleton<IAccountStatusCatalog>(new Catalog());
            services.AddSingleton<IAdminUserStatusAudit>(new Audit());
        }));

    private sealed class Session : ISessionService
    {
        public Task<SessionResult> GetSessionAsync(string token, CancellationToken ct) => throw new NotSupportedException();
        public Task<SessionAuthorization> AuthenticateAsync(string token, CancellationToken ct) =>
            Task.FromResult(new SessionAuthorization(SessionOutcome.Success,
                new LocalLoginProfile("admin", "admin-sub", "admin@uttt.edu.mx", "Active", ["Admin"], "Admin")));
    }
    private sealed class Writer(Target target) : IAdminUserStatusWriter
    {
        public Task<AdminUserStatusTarget?> FindAsync(string id, CancellationToken ct) =>
            Task.FromResult<AdminUserStatusTarget?>(id == target.Id ? new(target.Id, "target-sub", target.Status, null) : null);
        public Task<AdminUserStatusWriteResult> SetStatusAsync(string id, string expected, string next, CancellationToken ct) =>
            Task.FromResult(new AdminUserStatusWriteResult(AdminUserStatusWriteOutcome.Updated,
                new(id, "target-sub", next, null), expected));
    }
    private sealed record Target(string Id, string Status);
    private sealed class Gate : IAdminUserStatusRateLimitGate { public Task<bool> IsAllowedAsync(string id, CancellationToken ct) => Task.FromResult(true); }
    private sealed class Revoker : IAccountSessionRevoker { public Task RevokeAllAsync(string id, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Devices : IDeviceTokenStatusRemover { public Task RemoveForUserAsync(string id, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Catalog : IAccountStatusCatalog { public Task PauseProductsForSellerAsync(string id, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Audit : IAdminUserStatusAudit { public Task RecordAsync(AdminUserStatusAudit audit, CancellationToken ct) => Task.CompletedTask; }
}
