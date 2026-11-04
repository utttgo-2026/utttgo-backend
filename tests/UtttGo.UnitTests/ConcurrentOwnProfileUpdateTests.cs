using FluentAssertions;
using UTTTGO.Api.Modules.Identity.Application;
using Xunit;

namespace UtttGo.UnitTests;

public sealed class ConcurrentOwnProfileUpdateTests
{
    [Fact]
    public async Task SimultaneousUpdatesFromSameVersionHaveOneWinnerAndNoSilentOverwrite()
    {
        var original = new LocalLoginProfile("local", "sub", "a@uttt.edu.mx", "Active",
            ["Buyer"], "Original", "1234567890", "asset-1", 4);
        var store = new AtomicStore(original);
        var session = new SynchronizedSession(store);
        var service = new UpdateOwnProfileService(session, store, new NoMedia());

        // Both calls must read version 4 before either may issue its conditional update.
        var first = service.UpdateAsync("token", new(true, "First", false, null), default);
        var second = service.UpdateAsync("token", new(true, "Second", false, null), default);
        var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10));

        results.Count(result => result.Outcome == UpdateOwnProfileOutcome.Success).Should().Be(1);
        results.Count(result => result.Outcome == UpdateOwnProfileOutcome.Conflict).Should().Be(1);
        session.ObservedVersions.Should().Equal(4, 4);
        store.Version.Should().Be(5);
        var winner = results.Single(result => result.Outcome == UpdateOwnProfileOutcome.Success);
        store.Snapshot.DisplayName.Should().Be(winner.Response!.DisplayName)
            .And.BeOneOf("First", "Second");
        store.Snapshot.Phone.Should().Be(original.Phone);
        store.Snapshot.AvatarMediaAssetId.Should().Be(original.AvatarMediaAssetId);
        store.CommittedWrites.Should().Be(1);
    }

    private sealed class SynchronizedSession(AtomicStore store) : ISessionService
    {
        private readonly TaskCompletionSource _bothRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;
        public List<long> ObservedVersions { get; } = [];

        public async Task<SessionAuthorization> AuthenticateAsync(string token, CancellationToken cancellationToken)
        {
            var snapshot = store.Snapshot;
            lock (ObservedVersions) ObservedVersions.Add(snapshot.Version);
            if (Interlocked.Increment(ref _arrivals) == 2) _bothRead.TrySetResult();
            await _bothRead.Task.WaitAsync(cancellationToken);
            return new(SessionOutcome.Success, snapshot);
        }

        public Task<SessionResult> GetSessionAsync(string token, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class AtomicStore(LocalLoginProfile initial) : IProfileUpdateRepository
    {
        private readonly object _sync = new();
        private LocalLoginProfile _profile = initial;
        private int _writes;
        public LocalLoginProfile Snapshot { get { lock (_sync) return _profile; } }
        public long Version { get { lock (_sync) return _profile.Version; } }
        public int CommittedWrites { get { lock (_sync) return _writes; } }

        public Task<ProfileUpdateResult> UpdateOwnAsync(string subject, string localId, long expectedVersion,
            string? displayName, bool changeDisplayName, string? phone, bool changePhone,
            CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                if (_profile.SupabaseUserId != subject || _profile.Id != localId ||
                    _profile.AccountStatus != "Active")
                    return Task.FromResult(new ProfileUpdateResult(ProfileUpdateOutcome.Missing));
                if (_profile.Version != expectedVersion)
                    return Task.FromResult(new ProfileUpdateResult(ProfileUpdateOutcome.Conflict));
                _profile = _profile with
                {
                    DisplayName = changeDisplayName ? displayName : _profile.DisplayName,
                    Phone = changePhone ? phone : _profile.Phone,
                    Version = _profile.Version + 1
                };
                _writes++;
                return Task.FromResult(new ProfileUpdateResult(ProfileUpdateOutcome.Updated, _profile));
            }
        }
    }

    private sealed class NoMedia : IApprovedAvatarReadAccess
    {
        public Task<string?> GetApprovedOwnedTemporaryUrlAsync(string mediaAssetId, string ownerId,
            CancellationToken cancellationToken) => Task.FromResult<string?>(null);
    }
}
