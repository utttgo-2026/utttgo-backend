using FluentAssertions;
using UTTTGO.Api.Modules.Identity.Application;
using Xunit;

namespace UtttGo.UnitTests;

public sealed class OwnProfileServiceTests
{
    private static readonly LocalLoginProfile Profile = new("own-id", "trusted-sub", "a@uttt.edu.mx",
        "Active", ["Buyer"], "Alumna", "1234567890", "asset-1");

    [Fact]
    public async Task OwnProfileHasOnlyAllowedFieldsAndUsesMediaOwner()
    {
        var media = new MediaDouble("https://private.example/temporary-sas");
        var result = await new OwnProfileService(new SessionDouble(SessionOutcome.Success, Profile), media)
            .GetAsync("token", default);
        result.Outcome.Should().Be(SessionOutcome.Success);
        result.Response!.Id.Should().Be("own-id");
        result.Response.Phone.Should().Be("1234567890");
        result.Response.AvatarUrl.Should().Be("https://private.example/temporary-sas");
        result.Response.Roles.Should().Equal("Buyer");
        media.RequestedAsset.Should().Be("asset-1");
        media.RequestedOwner.Should().Be("own-id");
    }

    [Fact]
    public async Task NoAvatarAssetDoesNotConsultMedia()
    {
        var media = new MediaDouble("https://private.example/temporary-sas");
        var result = await new OwnProfileService(new SessionDouble(SessionOutcome.Success,
            Profile with { AvatarMediaAssetId = null }), media).GetAsync("token", default);
        result.Response!.AvatarUrl.Should().BeNull();
        media.RequestedAsset.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("PendingUpload")]
    public async Task UnapprovedOrUnavailableMediaReturnsNull(string? url)
    {
        var media = new MediaDouble(url == "PendingUpload" ? null : url, fail: url == "PendingUpload");
        var result = await new OwnProfileService(new SessionDouble(SessionOutcome.Success, Profile), media)
            .GetAsync("token", default);
        result.Response!.AvatarUrl.Should().BeNull();
    }

    [Theory]
    [InlineData(SessionOutcome.ProfileMissing, SessionOutcome.Unauthorized)]
    [InlineData(SessionOutcome.Unauthorized, SessionOutcome.Unauthorized)]
    [InlineData(SessionOutcome.Unavailable, SessionOutcome.Unavailable)]
    public async Task FailedAuthorizationCannotReadProfileOrMedia(SessionOutcome input, SessionOutcome expected)
    {
        var media = new MediaDouble(null);
        var result = await new OwnProfileService(new SessionDouble(input), media)
            .GetAsync("token", default);
        result.Outcome.Should().Be(expected);
        result.Response.Should().BeNull();
        media.RequestedAsset.Should().BeNull();
    }

    private sealed class SessionDouble(SessionOutcome outcome, LocalLoginProfile? profile = null) : ISessionService
    {
        public Task<SessionAuthorization> AuthenticateAsync(string token, CancellationToken cancellationToken) =>
            Task.FromResult(new SessionAuthorization(outcome, profile));

        public Task<SessionResult> GetSessionAsync(string token, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class MediaDouble(string? url, bool fail = false) : IApprovedAvatarReadAccess
    {
        public string? RequestedAsset { get; private set; }
        public string? RequestedOwner { get; private set; }

        public Task<string?> GetApprovedOwnedTemporaryUrlAsync(string mediaAssetId, string ownerId,
            CancellationToken cancellationToken)
        {
            RequestedAsset = mediaAssetId;
            RequestedOwner = ownerId;
            return fail ? throw new InvalidOperationException("media unavailable") : Task.FromResult(url);
        }
    }
}
