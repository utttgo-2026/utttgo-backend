using FluentAssertions;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Identity.Infrastructure;
using Xunit;

namespace UtttGo.UnitTests;

public sealed class AuthenticatedLocalProfileReaderTests
{
    [Fact]
    public async Task ValidSessionReturnsLocalProfileWithItsStoredAccountStatus()
    {
        var reader = Reader(profile: Profile("Active"));

        var result = await reader.ReadAsync("token", default);

        result.Outcome.Should().Be(AuthenticatedLocalProfileOutcome.Success);
        result.Profile!.Id.Should().Be("local-id");
        result.Profile.AccountStatus.Should().Be("Active");
    }

    [Fact]
    public async Task NonActiveAccountIsStillReturnedSoTheCallerCanAnswer403()
    {
        var reader = Reader(profile: Profile("PendingEmailVerification"));

        var result = await reader.ReadAsync("token", default);

        result.Outcome.Should().Be(AuthenticatedLocalProfileOutcome.Success);
        result.Profile!.AccountStatus.Should().Be("PendingEmailVerification");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task MissingSubjectIsUnauthorized(string? subject)
    {
        var reader = Reader(subject: subject);

        var result = await reader.ReadAsync("token", default);

        result.Outcome.Should().Be(AuthenticatedLocalProfileOutcome.Unauthorized);
    }

    [Fact]
    public async Task RevokedOrInvalidTokenIsUnauthorized()
    {
        var reader = Reader(gateAllowed: false);

        var result = await reader.ReadAsync("token", default);

        result.Outcome.Should().Be(AuthenticatedLocalProfileOutcome.Unauthorized);
    }

    [Fact]
    public async Task UnconfirmedEmailIsUnauthorized()
    {
        var reader = Reader(confirmed: false);

        var result = await reader.ReadAsync("token", default);

        result.Outcome.Should().Be(AuthenticatedLocalProfileOutcome.Unauthorized);
    }

    [Fact]
    public async Task MissingLocalProfileIsReportedAsProfileMissing()
    {
        var reader = Reader(missingProfile: true);

        var result = await reader.ReadAsync("token", default);

        result.Outcome.Should().Be(AuthenticatedLocalProfileOutcome.ProfileMissing);
    }

    [Fact]
    public async Task CriticalFailureIsUnavailable()
    {
        var reader = Reader(failure: "redis");

        var result = await reader.ReadAsync("token", default);

        result.Outcome.Should().Be(AuthenticatedLocalProfileOutcome.Unavailable);
    }

    private static LocalLoginProfile Profile(string status) =>
        new("local-id", "sub-1", "a@uttt.edu.mx", status, ["Buyer"], "Alumna");

    private static AuthenticatedLocalProfileReader Reader(string? subject = "sub-1", bool gateAllowed = true,
        bool confirmed = true, LocalLoginProfile? profile = null, bool missingProfile = false,
        string? failure = null) =>
        new(new SubjectDouble(subject, failure == "subject"), new GateDouble(gateAllowed, failure == "redis"),
            new IdentityDouble(confirmed, failure == "supabase"),
            new ProfileDouble(missingProfile ? null : profile ?? Profile("Active"), failure == "mongo"));

    private sealed class SubjectDouble(string? value, bool fail) : IValidatedJwtSubjectReader
    {
        public Task<string?> ReadValidatedSubjectAsync(string token, CancellationToken cancellationToken) =>
            fail ? throw new InvalidOperationException("internal-secret") : Task.FromResult(value);
    }

    private sealed class GateDouble(bool allowed, bool fail) : ILoginJwtGate
    {
        public Task<bool> IsValidAndNotRevokedAsync(string token, string subject,
            CancellationToken cancellationToken) =>
            fail ? throw new InvalidOperationException("internal-secret") : Task.FromResult(allowed);
    }

    private sealed class IdentityDouble(bool confirmed, bool fail) : ISupabaseSessionIdentity
    {
        public Task<ConfirmedSessionIdentity?> FindBySubjectAsync(string subject,
            CancellationToken cancellationToken) =>
            fail ? throw new InvalidOperationException("internal-secret") :
                Task.FromResult<ConfirmedSessionIdentity?>(new("sub-1", "a@uttt.edu.mx", confirmed));
    }

    private sealed class ProfileDouble(LocalLoginProfile? profile, bool fail) : ILoginProfileRepository
    {
        public Task<LocalLoginProfile?> FindBySubjectAsync(string subject, CancellationToken cancellationToken) =>
            fail ? throw new InvalidOperationException("internal-secret") : Task.FromResult(profile);
    }
}
