using FluentAssertions;
using Microsoft.Extensions.Configuration;
using UTTTGO.Api.Modules.Admin.Application;
using UTTTGO.Api.Modules.Admin.Contracts;
using UTTTGO.Api.Modules.Admin.Validators;
using UTTTGO.Api.Modules.Identity.Application;
using Xunit;

namespace UtttGo.UnitTests;

public sealed class AdminUsersTests
{
    private static readonly DateTime Utc = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly AdminUserListEntry[] Profiles =
    [
        new("b", "Ana", ["Buyer"], "Active", Utc),
        new("a", "ana Sol", ["Buyer", "Seller"], "Suspended", Utc),
        new("c", "Beto", ["Admin"], "PendingEmailVerification", Utc.AddDays(1))
    ];

    [Theory]
    [InlineData(null, null, null, 3)]
    [InlineData("ANA", null, null, 2)]
    [InlineData(null, "Seller", null, 1)]
    [InlineData("ana", "Buyer", "Suspended", 1)]
    [InlineData("ana", "Admin", null, 0)]
    public void SelectionCountsFilteredMatches(string? search, string? role, string? status, int total)
    {
        var page = AdminUserListSelection.Select(Profiles, new(search, role, status, 1, 1, true));
        page.Total.Should().Be(total);
        page.Items.Should().HaveCount(Math.Min(total, 1));
    }

    [Fact]
    public void SortingUsesCreatedAtAndStableIdTieBreaker()
    {
        var descending = AdminUserListSelection.Select(Profiles, new(null, null, null, 1, 3, true));
        descending.Items.Select(user => user.Id).Should().Equal("c", "a", "b");
        var ascending = AdminUserListSelection.Select(Profiles, new(null, null, null, 2, 2, false));
        ascending.Items.Select(user => user.Id).Should().Equal("c");
    }

    [Theory]
    [InlineData(null, "1", "Active", false)]
    [InlineData("1", "1", "Active", true)]
    [InlineData("0", "1", "Active", false)]
    [InlineData("1", "0", "Active", false)]
    [InlineData("1", "51", "Active", false)]
    [InlineData("1", "1", "Deleted", false)]
    [InlineData("2147483647", "50", "Active", false)]
    public async Task ValidatorChecksPagingAndEnums(string? page, string size, string status, bool valid)
    {
        var validator = new AdminUsersQueryRequestValidator(new ConfigurationBuilder().Build());
        var result = await validator.ValidateAsync(new AdminUsersQueryRequest(null, null, status, page, size, null));
        result.IsValid.Should().Be(valid);
    }

    [Fact]
    public async Task ValidatorRejectsOverlongSearchAndInvalidConfiguration()
    {
        var validator = new AdminUsersQueryRequestValidator(new ConfigurationBuilder().Build());
        (await validator.ValidateAsync(new AdminUsersQueryRequest(new string('a', 101), null, null, "1", "1", null)))
            .IsValid.Should().BeFalse();
        var configured = new AdminUsersQueryRequestValidator(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AdminUsers:MaxPageSize"] = "10" }).Build());
        (await configured.ValidateAsync(new AdminUsersQueryRequest(null, null, null, "1", "11", null)))
            .IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("Buyer", "Active", AdminUsersOutcome.Forbidden)]
    [InlineData("Admin", "Suspended", AdminUsersOutcome.Unauthorized)]
    [InlineData("Admin", "Active", AdminUsersOutcome.Success)]
    public async Task ServiceRequiresLocalActiveAdmin(string role, string status, AdminUsersOutcome expected)
    {
        var store = new Reader();
        var audit = new Audit();
        var service = new AdminUsersService(new Session(role, status), new Gate(), store, audit);
        var result = await service.ListAsync("jwt", new(null, null, null, 1, 2, true), "corr", default);
        result.Outcome.Should().Be(expected);
        store.Calls.Should().Be(expected == AdminUsersOutcome.Success ? 1 : 0);
        audit.Calls.Should().Be(expected == AdminUsersOutcome.Success ? 1 : 0);
    }

    [Theory]
    [InlineData("rate", AdminUsersOutcome.RateLimited)]
    [InlineData("redis", AdminUsersOutcome.Unavailable)]
    [InlineData("mongo", AdminUsersOutcome.Unavailable)]
    [InlineData("audit", AdminUsersOutcome.Unavailable)]
    public async Task CriticalFailuresNeverReturnPage(string failure, AdminUsersOutcome expected)
    {
        var result = await new AdminUsersService(new Session("Admin", "Active"),
            new Gate(failure), new Reader(failure), new Audit(failure))
            .ListAsync("jwt", new(null, null, null, 1, 1, true), "corr", default);
        result.Outcome.Should().Be(expected);
        result.Response.Should().BeNull();
    }

    private sealed class Session(string role, string status) : ISessionService
    {
        public Task<SessionResult> GetSessionAsync(string token, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SessionAuthorization> AuthenticateAsync(string token, CancellationToken cancellationToken) =>
            Task.FromResult(new SessionAuthorization(SessionOutcome.Success,
                new LocalLoginProfile("actor", "sub", "private@uttt.edu.mx", status, [role], "Actor")));
    }

    private sealed class Gate(string? failure = null) : IAdminUsersRateLimitGate
    {
        public Task<bool> IsAllowedAsync(string actorId, CancellationToken cancellationToken) =>
            failure == "redis" ? throw new InvalidOperationException() : Task.FromResult(failure != "rate");
    }

    private sealed class Reader(string? failure = null) : IAdminUserListReader
    {
        public int Calls { get; private set; }
        public Task<AdminUserPage> ListAsync(AdminUserListQuery query, CancellationToken cancellationToken)
        {
            Calls++;
            if (failure == "mongo") throw new InvalidOperationException();
            return Task.FromResult(AdminUserListSelection.Select(Profiles, query));
        }
    }

    private sealed class Audit(string? failure = null) : IAdminUsersAudit
    {
        public int Calls { get; private set; }
        public Task RecordAsync(string actorId, string correlationId, string result, CancellationToken cancellationToken)
        {
            Calls++;
            if (failure == "audit") throw new InvalidOperationException();
            return Task.CompletedTask;
        }
    }
}
