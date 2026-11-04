using FluentAssertions;
using UTTTGO.Api.Modules.Identity.Application;
using Xunit;

namespace UtttGo.UnitTests;

public sealed class SessionServiceTests
{
    private static readonly LocalLoginProfile Active =
        new("local", "sub-1", "a@uttt.edu.mx", "Active", ["Buyer"], "Alumna");
    private static readonly ConfirmedSessionIdentity Confirmed = new("sub-1", "a@uttt.edu.mx", true);

    [Fact]
    public async Task ValidSessionReturnsOnlyLocalIdentity()
    {
        var result = await Service().GetSessionAsync("test-token", default);
        result.Outcome.Should().Be(SessionOutcome.Success);
        result.Response!.Id.Should().Be("local");
        result.Response.Email.Should().Be("a@uttt.edu.mx");
        result.Response.DisplayName.Should().Be("Alumna");
        result.Response.Roles.Should().Equal("Buyer");
    }

    [Theory]
    [InlineData(null, true, true)]
    [InlineData("sub-1", false, true)]
    [InlineData("sub-1", true, false)]
    public async Task UnverifiedSubjectOrRevocationOrUnconfirmedEmailIsRejected(
        string? subject, bool gateAllowed, bool confirmed)
    {
        var result = await Service(subject: subject, gateAllowed: gateAllowed,
            identity: Confirmed with { EmailConfirmed = confirmed }).GetSessionAsync("test-token", default);
        result.Outcome.Should().Be(SessionOutcome.Unauthorized);
        result.Response.Should().BeNull();
    }

    [Fact]
    public async Task MissingProfileIsNotCreated()
    {
        var result = await Service(profile: null, missingProfile: true).GetSessionAsync("test-token", default);
        result.Outcome.Should().Be(SessionOutcome.ProfileMissing);
    }

    [Theory]
    [InlineData("PendingEmailVerification")]
    [InlineData("Suspended")]
    [InlineData("Deleted")]
    public async Task InactiveAccountIsRejected(string status)
    {
        var result = await Service(profile: Active with { AccountStatus = status })
            .GetSessionAsync("test-token", default);
        result.Outcome.Should().Be(SessionOutcome.Unauthorized);
    }

    [Fact]
    public async Task IdentityMismatchIsRejected()
    {
        (await Service(profile: Active with { SupabaseUserId = "other" }).GetSessionAsync("test-token", default))
            .Outcome.Should().Be(SessionOutcome.Unauthorized);
        (await Service(identity: Confirmed with { Email = "other@uttt.edu.mx" })
            .GetSessionAsync("test-token", default)).Outcome.Should().Be(SessionOutcome.Unauthorized);
        (await Service(identity: Confirmed with { Subject = "other" })
            .GetSessionAsync("test-token", default)).Outcome.Should().Be(SessionOutcome.Unauthorized);
    }

    [Theory]
    [InlineData("subject")]
    [InlineData("redis")]
    [InlineData("supabase")]
    [InlineData("mongo")]
    public async Task CriticalDependencyFailureIsFailClosed(string failure)
    {
        var result = await Service(failure: failure).GetSessionAsync("test-token", default);
        result.Outcome.Should().Be(SessionOutcome.Unavailable);
        result.Response.Should().BeNull();
    }

    private static SessionService Service(string? subject = "sub-1", bool gateAllowed = true,
        ConfirmedSessionIdentity? identity = null, LocalLoginProfile? profile = null,
        bool missingProfile = false, string? failure = null) => new(
            new SubjectDouble(subject, failure == "subject"), new GateDouble(gateAllowed, failure == "redis"),
            new IdentityDouble(identity ?? Confirmed, failure == "supabase"),
            new ProfileDouble(missingProfile ? null : profile ?? Active, failure == "mongo"));

    private sealed class SubjectDouble(string? subject, bool fail) : IValidatedJwtSubjectReader
    {
        public Task<string?> ReadValidatedSubjectAsync(string token, CancellationToken cancellationToken) =>
            fail ? throw new InvalidOperationException() : Task.FromResult(subject);
    }

    private sealed class GateDouble(bool allowed, bool fail) : ILoginJwtGate
    {
        public Task<bool> IsValidAndNotRevokedAsync(string token, string subject, CancellationToken cancellationToken) =>
            fail ? throw new InvalidOperationException() : Task.FromResult(allowed);
    }

    private sealed class IdentityDouble(ConfirmedSessionIdentity? identity, bool fail) : ISupabaseSessionIdentity
    {
        public Task<ConfirmedSessionIdentity?> FindBySubjectAsync(string subject, CancellationToken cancellationToken) =>
            fail ? throw new InvalidOperationException() : Task.FromResult(identity);
    }

    private sealed class ProfileDouble(LocalLoginProfile? profile, bool fail) : ILoginProfileRepository
    {
        public Task<LocalLoginProfile?> FindBySubjectAsync(string subject, CancellationToken cancellationToken) =>
            fail ? throw new InvalidOperationException() : Task.FromResult(profile);
    }
}
