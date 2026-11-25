using FluentAssertions;
using UTTTGO.Api.Modules.Admin.Application;
using UTTTGO.Api.Modules.Catalog.Application;
using UTTTGO.Api.Modules.Identity.Application;
using Xunit;

namespace UtttGo.UnitTests;

public sealed class AdminUserStatusServiceTests
{
    [Fact]
    public async Task SuspendingUserRevokesTokensRemovesDevicesPausesSellerAndAudits()
    {
        var writer = new Writer(new("target", "supabase-target", "Active", "seller"));
        var revoker = new Revoker(); var devices = new Devices(); var catalog = new Catalog(); var audit = new Audit();
        var service = new AdminUserStatusService(new Session(), new Gate(), writer, revoker, devices, catalog, audit);

        var result = await service.SetAsync("token", "target", "Suspended", "corr", CancellationToken.None);

        result.Outcome.Should().Be(AdminUserStatusOutcome.Success);
        revoker.UserId.Should().Be("supabase-target"); devices.UserId.Should().Be("target");
        catalog.SellerId.Should().Be("seller"); audit.Record.Should().NotBeNull();
    }

    [Fact]
    public async Task SameStatusIsIdempotentWithoutSideEffects()
    {
        var writer = new Writer(new("target", "supabase-target", "Active", null));
        var revoker = new Revoker(); var devices = new Devices(); var catalog = new Catalog(); var audit = new Audit();
        var result = await new AdminUserStatusService(new Session(), new Gate(), writer, revoker, devices, catalog, audit)
            .SetAsync("token", "target", "Active", "corr", CancellationToken.None);
        result.Outcome.Should().Be(AdminUserStatusOutcome.Success);
        revoker.UserId.Should().BeNull(); devices.UserId.Should().BeNull(); audit.Record.Should().BeNull();
    }

    [Fact]
    public async Task SelfTargetIsRejectedBeforeWriting()
    {
        var writer = new Writer(new("admin", "supabase-target", "Active", null));
        var result = await new AdminUserStatusService(new Session(), new Gate(), writer,
            new Revoker(), new Devices(), new Catalog(), new Audit())
            .SetAsync("token", "admin", "Suspended", "corr", CancellationToken.None);

        result.Outcome.Should().Be(AdminUserStatusOutcome.SelfTarget);
        writer.Writes.Should().Be(0);
    }

    [Fact]
    public async Task DependencyFailureIsUnavailableWithoutReturningSuccess()
    {
        var writer = new Writer(new("target", "supabase-target", "Active", "seller"));
        var result = await new AdminUserStatusService(new Session(), new Gate(), writer,
            new ThrowingRevoker(), new Devices(), new Catalog(), new Audit())
            .SetAsync("token", "target", "Suspended", "corr", CancellationToken.None);

        result.Outcome.Should().Be(AdminUserStatusOutcome.Unavailable);
    }

    private sealed class Session : ISessionService
    {
        public Task<SessionResult> GetSessionAsync(string token, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<SessionAuthorization> AuthenticateAsync(string token, CancellationToken cancellationToken) =>
            Task.FromResult(new SessionAuthorization(SessionOutcome.Success,
                new LocalLoginProfile("admin", "admin-sub", "admin@uttt.edu.mx", "Active", ["Admin"], "Admin")));
    }
    private sealed class Gate : IAdminUserStatusRateLimitGate { public Task<bool> IsAllowedAsync(string actorId, CancellationToken ct) => Task.FromResult(true); }
    private sealed class Writer(AdminUserStatusTarget target) : IAdminUserStatusWriter
    {
        public int Writes { get; private set; }
        public Task<AdminUserStatusTarget?> FindAsync(string id, CancellationToken ct) => Task.FromResult<AdminUserStatusTarget?>(target.Id == id ? target : null);
        public Task<AdminUserStatusWriteResult> SetStatusAsync(string id, string expected, string next, CancellationToken ct) =>
            Task.FromResult(Write(id, expected, next));
        private AdminUserStatusWriteResult Write(string id, string expected, string next)
        {
            Writes++;
            return new(AdminUserStatusWriteOutcome.Updated, target with { AccountStatus = next }, expected);
        }
    }
    private sealed class Revoker : IAccountSessionRevoker { public string? UserId; public Task RevokeAllAsync(string id, CancellationToken ct) { UserId = id; return Task.CompletedTask; } }
    private sealed class Devices : IDeviceTokenStatusRemover { public string? UserId; public Task RemoveForUserAsync(string id, CancellationToken ct) { UserId = id; return Task.CompletedTask; } }
    private sealed class Catalog : IAccountStatusCatalog { public string? SellerId; public Task PauseProductsForSellerAsync(string id, CancellationToken ct) { SellerId = id; return Task.CompletedTask; } }
    private sealed class Audit : IAdminUserStatusAudit { public AdminUserStatusAudit? Record; public Task RecordAsync(AdminUserStatusAudit audit, CancellationToken ct) { Record = audit; return Task.CompletedTask; } }
    private sealed class ThrowingRevoker : IAccountSessionRevoker
    {
        public Task RevokeAllAsync(string id, CancellationToken ct) => throw new InvalidOperationException();
    }
}
