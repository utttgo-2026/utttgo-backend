using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UTTTGO.Api.Modules.Admin.Application;
using UTTTGO.Api.Modules.Identity.Application;
using Xunit;

namespace UtttGo.SecurityTests;

public sealed class AdminUsersSecurityTests
{
    [Theory]
    [InlineData(null, "Admin", "Active", SessionOutcome.Success, HttpStatusCode.Unauthorized)]
    [InlineData("jwt", "Buyer", "Active", SessionOutcome.Success, HttpStatusCode.Forbidden)]
    [InlineData("jwt", "Seller", "Active", SessionOutcome.Success, HttpStatusCode.Forbidden)]
    [InlineData("jwt", "Admin", "Suspended", SessionOutcome.Success, HttpStatusCode.Unauthorized)]
    [InlineData("jwt", "Admin", "PendingEmailVerification", SessionOutcome.Success, HttpStatusCode.Unauthorized)]
    [InlineData("jwt", "Admin", "Active", SessionOutcome.Unauthorized, HttpStatusCode.Unauthorized)]
    [InlineData("jwt", "Admin", "Active", SessionOutcome.Unavailable, HttpStatusCode.ServiceUnavailable)]
    public async Task AuthorizationNeverLeaksUserList(string? token, string role, string status,
        SessionOutcome outcome, HttpStatusCode expected)
    {
        var reader = new Reader();
        var audit = new Audit();
        using var factory = Factory(new Session(role, status, outcome), reader, audit);
        using var client = factory.CreateClient();
        if (token is not null) client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.GetAsync("/api/v1/admin/users?page=1&pageSize=1&role=Admin");
        response.StatusCode.Should().Be(expected);
        reader.Calls.Should().Be(0);
        audit.Calls.Should().Be(0);
        (await response.Content.ReadAsStringAsync()).Should().Contain("correlationId")
            .And.NotContain("private@uttt.edu.mx").And.NotContain("jwt");
    }

    [Fact]
    public async Task FakeAdminQueryAndUntrustedRoleNeverGrantsAccess()
    {
        var reader = new Reader();
        using var factory = Factory(new Session("Buyer", "Active", SessionOutcome.Success), reader, new Audit());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "jwt");
        using var response = await client.GetAsync("/api/v1/admin/users?page=1&pageSize=1&role=Admin&sub=admin");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        reader.Calls.Should().Be(0);
    }

    [Fact]
    public async Task AuditFailureDoesNotReturnUsers()
    {
        var reader = new Reader();
        using var factory = Factory(new Session("Admin", "Active", SessionOutcome.Success), reader,
            new Audit(fail: true));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "jwt");
        using var response = await client.GetAsync("/api/v1/admin/users?page=1&pageSize=1");
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        reader.Calls.Should().Be(1);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("items").And.NotContain("email")
            .And.NotContain("private@uttt.edu.mx");
    }

    private static WebApplicationFactory<Program> Factory(ISessionService session, Reader reader, Audit audit) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISessionService>();
            services.RemoveAll<IAdminUserListReader>();
            services.RemoveAll<IAdminUsersRateLimitGate>();
            services.RemoveAll<IAdminUsersAudit>();
            services.AddSingleton(session);
            services.AddSingleton<IAdminUserListReader>(reader);
            services.AddSingleton<IAdminUsersRateLimitGate>(new Gate());
            services.AddSingleton<IAdminUsersAudit>(audit);
        }));

    private sealed class Session(string role, string status, SessionOutcome outcome) : ISessionService
    {
        public Task<SessionResult> GetSessionAsync(string token, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SessionAuthorization> AuthenticateAsync(string token, CancellationToken cancellationToken) =>
            Task.FromResult(new SessionAuthorization(outcome,
                new LocalLoginProfile("actor", "sub", "private@uttt.edu.mx", status, [role], "Actor")));
    }

    private sealed class Reader : IAdminUserListReader
    {
        public int Calls { get; private set; }
        public Task<AdminUserPage> ListAsync(AdminUserListQuery query, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new AdminUserPage([], 0));
        }
    }

    private sealed class Gate : IAdminUsersRateLimitGate
    {
        public Task<bool> IsAllowedAsync(string actorId, CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }

    private sealed class Audit(bool fail = false) : IAdminUsersAudit
    {
        public int Calls { get; private set; }
        public Task RecordAsync(string actorId, string correlationId, string result,
            CancellationToken cancellationToken)
        {
            Calls++;
            return fail ? throw new InvalidOperationException("private@uttt.edu.mx") : Task.CompletedTask;
        }
    }
}
