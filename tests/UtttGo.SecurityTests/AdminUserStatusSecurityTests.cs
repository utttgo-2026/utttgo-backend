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

namespace UtttGo.SecurityTests;

public sealed class AdminUserStatusSecurityTests
{
    [Theory]
    [InlineData(null, "Admin", "Active", HttpStatusCode.Unauthorized)]
    [InlineData("token", "Buyer", "Active", HttpStatusCode.Forbidden)]
    [InlineData("token", "Admin", "Active", HttpStatusCode.Conflict)]
    [InlineData("token", "Admin", "Suspended", HttpStatusCode.Unauthorized)]
    public async Task ActorMustBeAnActiveAdminAndCannotTargetSelf(string? token, string role, string status,
        HttpStatusCode expected)
    {
        using var factory = Factory(new Session(role, status), new Writer());
        using var client = factory.CreateClient();
        if (token is not null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.PatchAsJsonAsync("/api/v1/admin/users/actor/status",
            new { accountStatus = "Suspended" });
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task RedisOrAuditFailureFailsClosed()
    {
        using var factory = Factory(new Session("Admin", "Active"), new Writer(), fail: true);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "token");
        using var response = await client.PatchAsJsonAsync("/api/v1/admin/users/target/status",
            new { accountStatus = "Suspended" });
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).Should().Contain("correlationId").And.NotContain("token");
    }

    private static WebApplicationFactory<Program> Factory(Session session, Writer writer, bool fail = false) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISessionService>(); services.RemoveAll<IAdminUserStatusWriter>();
            services.RemoveAll<IAdminUserStatusRateLimitGate>(); services.RemoveAll<IAccountSessionRevoker>();
            services.RemoveAll<IDeviceTokenStatusRemover>(); services.RemoveAll<IAccountStatusCatalog>();
            services.RemoveAll<IAdminUserStatusAudit>();
            services.AddSingleton<ISessionService>(session); services.AddSingleton<IAdminUserStatusWriter>(writer);
            services.AddSingleton<IAdminUserStatusRateLimitGate>(new Gate(fail));
            services.AddSingleton<IAccountSessionRevoker>(new Revoker());
            services.AddSingleton<IDeviceTokenStatusRemover>(new Devices());
            services.AddSingleton<IAccountStatusCatalog>(new Catalog());
            services.AddSingleton<IAdminUserStatusAudit>(new Audit(fail));
        }));

    private sealed class Session(string role, string status) : ISessionService
    {
        public Task<SessionResult> GetSessionAsync(string token, CancellationToken ct) => throw new NotSupportedException();
        public Task<SessionAuthorization> AuthenticateAsync(string token, CancellationToken ct) =>
            Task.FromResult(new SessionAuthorization(SessionOutcome.Success,
                new LocalLoginProfile("actor", "actor-sub", "actor@uttt.edu.mx", status, [role], "Actor")));
    }
    private sealed class Writer : IAdminUserStatusWriter
    {
        public Task<AdminUserStatusTarget?> FindAsync(string id, CancellationToken ct) =>
            Task.FromResult<AdminUserStatusTarget?>(id == "actor" ? new("actor", "actor-sub", "Active", null) :
                new(id, "target-sub", "Active", null));
        public Task<AdminUserStatusWriteResult> SetStatusAsync(string id, string expected, string next, CancellationToken ct) =>
            Task.FromResult(new AdminUserStatusWriteResult(AdminUserStatusWriteOutcome.Updated,
                new(id, "target-sub", next, null), expected));
    }
    private sealed class Gate(bool fail) : IAdminUserStatusRateLimitGate
    { public Task<bool> IsAllowedAsync(string id, CancellationToken ct) => fail ? throw new InvalidOperationException() : Task.FromResult(true); }
    private sealed class Revoker : IAccountSessionRevoker { public Task RevokeAllAsync(string id, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Devices : IDeviceTokenStatusRemover { public Task RemoveForUserAsync(string id, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Catalog : IAccountStatusCatalog { public Task PauseProductsForSellerAsync(string id, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Audit(bool fail) : IAdminUserStatusAudit
    { public Task RecordAsync(AdminUserStatusAudit audit, CancellationToken ct) => fail ? throw new InvalidOperationException() : Task.CompletedTask; }
}
