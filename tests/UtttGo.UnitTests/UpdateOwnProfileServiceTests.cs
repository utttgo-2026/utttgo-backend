using System.Text.Json;
using FluentAssertions;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Identity.Contracts;
using UTTTGO.Api.Modules.Identity.Validators;
using Xunit;

namespace UtttGo.UnitTests;

public sealed class UpdateOwnProfileServiceTests
{
    private static readonly LocalLoginProfile Profile = new("local", "sub", "a@uttt.edu.mx", "Active",
        ["Buyer"], "Original", "+521234567890", "asset-1", 4);

    [Fact]
    public async Task DisplayNameOnlyPreservesPhoneAndAvatarReferenceAndUsesSubjectVersion()
    {
        var repository = new Repository(Profile);
        var result = await new UpdateOwnProfileService(new Session(Profile), repository, new Avatar())
            .UpdateAsync("token", new(true, " New Name ", false, null), default);
        result.Outcome.Should().Be(UpdateOwnProfileOutcome.Success);
        result.Response!.DisplayName.Should().Be("New Name");
        result.Response.Phone.Should().Be(Profile.Phone);
        result.Response.AvatarUrl.Should().BeNull();
        repository.Subject.Should().Be("sub");
        repository.LocalId.Should().Be("local");
        repository.ExpectedVersion.Should().Be(4);
        repository.Current!.AvatarMediaAssetId.Should().Be("asset-1");
    }

    [Fact]
    public async Task PhoneOnlyKeepsDisplayName()
    {
        var result = await new UpdateOwnProfileService(new Session(Profile), new Repository(Profile), new Avatar())
            .UpdateAsync("token", new(false, null, true, "1234567890"), default);
        result.Response!.DisplayName.Should().Be("Original");
        result.Response.Phone.Should().Be("1234567890");
    }

    [Theory]
    [InlineData(SessionOutcome.Unauthorized, UpdateOwnProfileOutcome.Unauthorized)]
    [InlineData(SessionOutcome.ProfileMissing, UpdateOwnProfileOutcome.Unauthorized)]
    [InlineData(SessionOutcome.Unavailable, UpdateOwnProfileOutcome.Unavailable)]
    public async Task AuthFailureNeverCallsRepository(SessionOutcome outcome, UpdateOwnProfileOutcome expected)
    {
        var repository = new Repository(Profile);
        var result = await new UpdateOwnProfileService(new Session(null, outcome), repository, new Avatar())
            .UpdateAsync("token", new(true, "Name", false, null), default);
        result.Outcome.Should().Be(expected);
        repository.Subject.Should().BeNull();
    }

    [Fact]
    public async Task ConcurrentVersionConflictDoesNotClaimSuccess()
    {
        var repository = new Repository(Profile with { Version = 5 });
        var result = await new UpdateOwnProfileService(new Session(Profile), repository, new Avatar())
            .UpdateAsync("token", new(true, "Name", false, null), default);
        result.Outcome.Should().Be(UpdateOwnProfileOutcome.Conflict);
    }

    [Fact]
    public async Task PersistenceFailureIsFailClosed()
    {
        var result = await new UpdateOwnProfileService(new Session(Profile),
            new Repository(Profile, fail: true), new Avatar())
            .UpdateAsync("token", new(true, "Name", false, null), default);
        result.Outcome.Should().Be(UpdateOwnProfileOutcome.Unavailable);
        result.Response.Should().BeNull();
    }

    [Fact]
    public async Task RepositoryCannotSmugglePrivilegedChangesIntoSuccess()
    {
        var repository = new ForgedRepository();
        var result = await new UpdateOwnProfileService(new Session(Profile), repository, new Avatar())
            .UpdateAsync("token", new(true, "Name", false, null), default);
        result.Outcome.Should().Be(UpdateOwnProfileOutcome.Unavailable);
        result.Response.Should().BeNull();
    }

    [Theory]
    [InlineData("{\"roles\":[\"Admin\"]}")]
    [InlineData("{\"accountStatus\":\"Active\"}")]
    [InlineData("{\"avatarUrl\":\"https://public.example/avatar\"}")]
    [InlineData("{\"mediaAssetId\":\"unapproved\"}")]
    [InlineData("{\"displayName\":null}")]
    [InlineData("{\"phone\":null}")]
    [InlineData("{\"displayName\":\"A\",\"DisplayName\":\"B\"}")]
    [InlineData("{}")]
    public void ForbiddenNullDuplicateAndEmptyPayloadsAreRejected(string json)
    {
        using var document = JsonDocument.Parse(json);
        UpdateOwnProfileRequest.TryParse(document.RootElement, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("", "1234567890")]
    [InlineData("Valid", "abcd")]
    [InlineData("Valid", "+123")]
    public void CandidateValidationRejectsBadValues(string name, string phone)
    {
        var request = new UpdateOwnProfileRequest(true, name, true, phone);
        new UpdateOwnProfileRequestValidator().Validate(request)
            .IsValid.Should().BeFalse();
    }

    private sealed class Session(LocalLoginProfile? profile, SessionOutcome outcome = SessionOutcome.Success)
        : ISessionService
    {
        public Task<SessionAuthorization> AuthenticateAsync(string token, CancellationToken cancellationToken) =>
            Task.FromResult(new SessionAuthorization(outcome, profile));
        public Task<SessionResult> GetSessionAsync(string token, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class Repository(LocalLoginProfile stored, bool fail = false) : IProfileUpdateRepository
    {
        public string? Subject { get; private set; }
        public string? LocalId { get; private set; }
        public long ExpectedVersion { get; private set; }
        public LocalLoginProfile? Current { get; private set; }

        public Task<ProfileUpdateResult> UpdateOwnAsync(string subject, string localId, long expectedVersion,
            string? displayName, bool changeDisplayName, string? phone, bool changePhone,
            CancellationToken cancellationToken)
        {
            Subject = subject;
            LocalId = localId;
            ExpectedVersion = expectedVersion;
            if (fail) throw new InvalidOperationException("database secret");
            if (stored.Version != expectedVersion)
                return Task.FromResult(new ProfileUpdateResult(ProfileUpdateOutcome.Conflict));
            Current = stored with
            {
                DisplayName = changeDisplayName ? displayName : stored.DisplayName,
                Phone = changePhone ? phone : stored.Phone,
                Version = stored.Version + 1
            };
            return Task.FromResult(new ProfileUpdateResult(ProfileUpdateOutcome.Updated, Current));
        }
    }

    private sealed class ForgedRepository : IProfileUpdateRepository
    {
        public Task<ProfileUpdateResult> UpdateOwnAsync(string subject, string localId, long expectedVersion,
            string? displayName, bool changeDisplayName, string? phone, bool changePhone,
            CancellationToken cancellationToken) => Task.FromResult(new ProfileUpdateResult(ProfileUpdateOutcome.Updated,
                Profile with { DisplayName = displayName, Roles = ["Admin"], Version = expectedVersion + 1 }));
    }

    private sealed class Avatar(string? url = null) : IApprovedAvatarReadAccess
    {
        public Task<string?> GetApprovedOwnedTemporaryUrlAsync(string mediaAssetId, string ownerId,
            CancellationToken cancellationToken) => Task.FromResult(url);
    }
}
