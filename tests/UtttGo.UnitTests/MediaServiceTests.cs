using FluentAssertions;
using Amazon.S3;
using System.Net;
using UTTTGO.Api.Modules.Media.Application;
using UTTTGO.Api.Modules.Media.Contracts;
using Xunit;

namespace UtttGo.UnitTests;

public sealed class MediaServiceTests
{
    private const string Id = "507f1f77bcf86cd799439011";
    private const string Owner = "owner";

    [Fact]
    public async Task UploadIntentRejectsInvalidMetadataAndPersistsOnlyAfterSigning()
    {
        var repo = new Repository();
        var store = new ObjectStore();
        var service = new MediaService(repo, store, new Moderator());
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.StartAsync(Owner, new PresignedUrlRequest
            { FileName = "a.jpg", MimeType = "text/plain", Size = 1 }, default));
        repo.Value.Should().BeNull();
        var intent = await service.StartAsync(Owner, new PresignedUrlRequest
        { FileName = "a.jpg", MimeType = "image/jpeg", Size = 10 }, default);
        intent.Headers["Content-Type"].Should().Be("image/jpeg");
        repo.Value!.Status.Should().Be(MediaStatus.PendingUpload);
        intent.MediaAssetId.Should().Be(repo.Value.Id);
    }

    [Theory]
    [InlineData(true, MediaStatus.Approved)]
    [InlineData(false, MediaStatus.Rejected)]
    public async Task ConfirmationRequiresHeadAndIsIdempotent(bool safe, string expected)
    {
        var repo = new Repository { Value = Asset() };
        var store = new ObjectStore { Head = (10, "image/jpeg") };
        var moderator = new Moderator { Safe = safe };
        var service = new MediaService(repo, store, moderator);
        (await service.ConfirmAsync(Id, "other", default)).Should().BeNull();
        (await service.ConfirmAsync(Id, Owner, default))!.Status.Should().Be(expected);
        (await service.ConfirmAsync(Id, Owner, default))!.Status.Should().Be(expected);
        moderator.Calls.Should().Be(1);
        store.Deletes.Should().Be(safe ? 0 : 1);
        (await service.IsApprovedForOwnerAsync(Id, "other", default)).Should().BeFalse();
        (await service.GetApprovedReadUrlAsync(Id, default) is not null).Should().Be(safe);
    }

    [Fact]
    public async Task MissingOrMismatchedObjectNeverStartsModeration()
    {
        var repo = new Repository { Value = Asset() };
        var store = new ObjectStore { Head = (11, "image/jpeg") };
        var moderator = new Moderator();
        var service = new MediaService(repo, store, moderator);
        var exception = await Assert.ThrowsAsync<MediaCompletionException>(() =>
            service.ConfirmAsync(Id, Owner, default));
        exception.Failure.Should().Be(MediaCompletionFailure.SizeMismatch);
        repo.Value!.Status.Should().Be(MediaStatus.PendingUpload);
        moderator.Calls.Should().Be(0);
    }

    [Fact]
    public async Task MissingObjectHasDistinctCompletionFailure()
    {
        var repo = new Repository { Value = Asset() };
        var service = new MediaService(repo, new ObjectStore(), new Moderator());

        var exception = await Assert.ThrowsAsync<MediaCompletionException>(() =>
            service.ConfirmAsync(Id, Owner, default));

        exception.Failure.Should().Be(MediaCompletionFailure.ObjectNotUploaded);
        repo.Value!.Status.Should().Be(MediaStatus.PendingUpload);
    }

    [Fact]
    public async Task InvalidContentPropagatesItsFailureAfterRejectingTheAsset()
    {
        var repo = new Repository { Value = Asset() };
        var store = new ObjectStore { Head = (10, "image/jpeg") };
        var service = new MediaService(repo, store, new InvalidImageModerator());

        var exception = await Assert.ThrowsAsync<MediaCompletionException>(() =>
            service.ConfirmAsync(Id, Owner, default));

        exception.Failure.Should().Be(MediaCompletionFailure.ContentInvalid);
        repo.Value!.Status.Should().Be(MediaStatus.Rejected);
        store.Deletes.Should().Be(1);
    }

    [Fact]
    public async Task UnconfiguredModeratorFailsClosedAndTerminalStateIsStable()
    {
        var repo = new Repository { Value = Asset() };
        var service = new MediaService(repo, new ObjectStore { Head = (10, "image/jpeg") },
            new UnconfiguredImageModerator());
        (await service.ConfirmAsync(Id, Owner, default))!.Status.Should().Be(MediaStatus.ProcessingFailed);
        (await service.ConfirmAsync(Id, Owner, default))!.Status.Should().Be(MediaStatus.ProcessingFailed);
        (await service.IsApprovedForOwnerAsync(Id, Owner, default)).Should().BeFalse();
    }

    [Fact]
    public async Task PendingModerationRemainsVisibleUntilTheProviderReturns()
    {
        var repo = new Repository { Value = Asset() };
        var moderator = new DelayedModerator();
        var service = new MediaService(repo, new ObjectStore { Head = (10, "image/jpeg") }, moderator);
        var confirmation = service.ConfirmAsync(Id, Owner, default);
        await moderator.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        (await service.ReadOwnedAsync(Id, Owner, default))!.Status.Should()
            .Be(MediaStatus.PendingModeration);
        confirmation.IsCompleted.Should().BeFalse();
        moderator.Complete(false);
        (await confirmation)!.Status.Should().Be(MediaStatus.Rejected);
    }

    [Fact]
    public async Task ConcurrentConfirmDoesNotRunSecondModerationOrChangeTerminalRejection()
    {
        var repo = new Repository { Value = Asset() };
        var moderator = new DelayedModerator();
        var store = new ObjectStore { Head = (10, "image/jpeg") };
        var service = new MediaService(repo, store, moderator);
        var first = service.ConfirmAsync(Id, Owner, default);
        await moderator.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        (await service.ConfirmAsync(Id, Owner, default))!.Status.Should().Be(MediaStatus.PendingModeration);
        moderator.Calls.Should().Be(1);
        moderator.Complete(false);
        (await first)!.Status.Should().Be(MediaStatus.Rejected);
        (await service.ConfirmAsync(Id, Owner, default))!.Status.Should().Be(MediaStatus.Rejected);
        moderator.Calls.Should().Be(1);
        store.Deletes.Should().Be(1);
    }

    [Fact]
    public async Task StalePendingModerationBecomesTerminalFailureWithoutAnotherAnalysis()
    {
        var repo = new Repository
        {
            Value = Asset() with
            {
                Status = MediaStatus.PendingModeration,
                Version = 1,
                ModerationStartedAt = DateTime.UtcNow.AddMinutes(-3)
            }
        };
        var moderator = new Moderator();
        var service = new MediaService(repo, new ObjectStore(), moderator);
        (await service.ConfirmAsync(Id, Owner, default))!.Status.Should().Be(MediaStatus.ProcessingFailed);
        (await service.ConfirmAsync(Id, Owner, default))!.Status.Should().Be(MediaStatus.ProcessingFailed);
        moderator.Calls.Should().Be(0);
        (await service.GetApprovedReadUrlAsync(Id, default)).Should().BeNull();
    }

    [Theory]
    [InlineData(MediaImagePolicy.MaxBytes, true)]
    [InlineData(MediaImagePolicy.MaxBytes + 1, false)]
    public async Task UploadIntentEnforcesDocumentedDecimalAzureLimit(int size, bool allowed)
    {
        var repo = new Repository();
        var service = new MediaService(repo, new ObjectStore(), new Moderator());
        var request = new PresignedUrlRequest { FileName = "food.png", MimeType = "image/png", Size = size };
        if (allowed)
            (await service.StartAsync(Owner, request, default)).MediaAssetId.Should().NotBeEmpty();
        else
            await Assert.ThrowsAsync<ArgumentException>(() => service.StartAsync(Owner, request, default));
    }

    [Fact]
    public async Task BatchReadSignsOnlyApprovedAssetsWithoutPersistingUrls()
    {
        var repo = new Repository { Value = Asset() with { Status = MediaStatus.Approved } };
        var service = new MediaService(repo, new ObjectStore(), new Moderator());

        var urls = await service.GetApprovedReadUrlsAsync([Id, Id, "missing"], default);

        urls.Should().ContainKey(Id);
        urls[Id].Url.Should().StartWith("https://local.invalid/");
        typeof(UTTTGO.Api.Modules.Catalog.Infrastructure.ProductDocument)
            .GetProperties().Select(property => property.Name).Should()
            .NotContain(name => name.Contains("Url", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ApprovedAssetIsPromotedIdempotentlyAndSourceIsDeletedAfterCas()
    {
        var repo = new Repository { Value = Asset() with { Status = MediaStatus.Approved, Version = 1 } };
        var store = new ObjectStore();
        store.Objects[$"staging/{Owner}/{Id}"] = (10, "image/jpeg");
        var service = new MediaService(repo, store, new Moderator());

        (await service.PromoteApprovedAsync(Id, Owner, default)).Should().BeTrue();
        (await service.PromoteApprovedAsync(Id, Owner, default)).Should().BeTrue();

        repo.Value!.ObjectKey.Should().Be($"approved/{Owner}/{Id}");
        store.Copies.Should().Be(1);
        store.DeletedKeys.Should().Contain($"staging/{Owner}/{Id}");
    }

    [Fact]
    public async Task MissingDestinationWithAccessDeniedHeadIsCopiedAndVerifiedAfterCopy()
    {
        var repo = new Repository { Value = Asset() with { Status = MediaStatus.Approved, Version = 1 } };
        var store = new ObjectStore { AccessDeniedWhenMissing = true };
        store.Objects[$"staging/{Owner}/{Id}"] = (10, "image/jpeg");
        var service = new MediaService(repo, store, new Moderator());

        (await service.PromoteApprovedAsync(Id, Owner, default)).Should().BeTrue();

        store.Copies.Should().Be(1);
        store.HeadedKeys.Should().ContainInOrder(
            $"staging/{Owner}/{Id}", $"approved/{Owner}/{Id}");
        repo.Value!.ObjectKey.Should().Be($"approved/{Owner}/{Id}");
    }

    [Fact]
    public async Task FailedPromotionCasCompensatesCreatedDestinationAndKeepsSource()
    {
        var repo = new Repository
        {
            Value = Asset() with { Status = MediaStatus.Approved, Version = 1 },
            PromoteSucceeds = false
        };
        var store = new ObjectStore();
        store.Objects[$"staging/{Owner}/{Id}"] = (10, "image/jpeg");
        var service = new MediaService(repo, store, new Moderator());

        (await service.PromoteApprovedAsync(Id, Owner, default)).Should().BeFalse();

        store.Copies.Should().Be(1);
        store.DeletedKeys.Should().Contain($"approved/{Owner}/{Id}");
        store.DeletedKeys.Should().NotContain($"staging/{Owner}/{Id}");
        repo.Value!.ObjectKey.Should().Be($"staging/{Owner}/{Id}");
    }

    [Fact]
    public async Task CopyFailureLeavesStagingAndDoesNotPromoteMongo()
    {
        var repo = new Repository { Value = Asset() with { Status = MediaStatus.Approved, Version = 1 } };
        var store = new ObjectStore { CopyFails = true };
        store.Objects[$"staging/{Owner}/{Id}"] = (10, "image/jpeg");
        var service = new MediaService(repo, store, new Moderator());

        await Assert.ThrowsAsync<AmazonS3Exception>(() => service.PromoteApprovedAsync(Id, Owner, default));

        repo.Value!.ObjectKey.Should().Be($"staging/{Owner}/{Id}");
        store.DeletedKeys.Should().NotContain($"staging/{Owner}/{Id}");
    }

    [Fact]
    public void S3ConfigurationDoesNotExposeExplicitCredentials()
    {
        typeof(UTTTGO.Api.Shared.Contracts.S3Config).GetProperties()
            .Select(property => property.Name).Should()
            .NotContain(name => name == "AccessKey" || name == "SecretKey");
    }

    [Theory]
    [InlineData("image/jpeg", true)]
    [InlineData("image/png", false)]
    [InlineData("image/webp", false)]
    public void MagicBytesMustMatchClaimedFormat(string mime, bool matches)
    {
        MediaImagePolicy.MatchesFormat(new byte[] { 0xff, 0xd8, 0xff, 0, 0, 0xff, 0xd9 }, mime)
            .Should().Be(matches);
    }

    private static MediaRecord Asset() =>
        new(Id, Owner, "staging/owner/" + Id, "image/jpeg", 10,
            MediaStatus.PendingUpload, DateTime.UtcNow);

    private sealed class Repository : IMediaRepository
    {
        public MediaRecord? Value;
        public bool PromoteSucceeds = true;
        public Task AddAsync(MediaRecord record, CancellationToken ct)
        {
            Value = record;
            return Task.CompletedTask;
        }
        public Task<MediaRecord?> FindAsync(string id, CancellationToken ct) =>
            Task.FromResult(Value?.Id == id ? Value : null);
        public Task<IReadOnlyList<MediaRecord>> ListForOwnerAsync(string owner, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<MediaRecord>>(Value?.OwnerUserId == owner ? [Value] : []);
        public Task<bool> TransitionAsync(string id, string owner, long version, string from, string to,
            CancellationToken ct)
        {
            if (Value is null || Value.Id != id || Value.OwnerUserId != owner ||
                Value.Version != version || Value.Status != from) return Task.FromResult(false);
            Value = Value with { Status = to, Version = version + 1 };
            return Task.FromResult(true);
        }
        public Task<bool> PromoteAsync(string id, string owner, long version, string sourceKey, string targetKey,
            CancellationToken ct)
        {
            if (Value is null || Value.Id != id || Value.OwnerUserId != owner ||
                Value.Version != version || Value.Status != MediaStatus.Approved || Value.ObjectKey != sourceKey ||
                !PromoteSucceeds)
                return Task.FromResult(false);
            Value = Value with { ObjectKey = targetKey, Version = version + 1 };
            return Task.FromResult(true);
        }
        public Task<bool> MarkDeletedAsync(string id, string owner, long version, CancellationToken ct)
        {
            if (Value is null || Value.Id != id || Value.OwnerUserId != owner || Value.Version != version)
                return Task.FromResult(false);
            Value = Value with { Status = MediaStatus.Deleted, Version = version + 1 };
            return Task.FromResult(true);
        }
    }

    private sealed class ObjectStore : IMediaObjectStore
    {
        public (long Length, string Mime)? Head;
        public int Deletes;
        public int Copies;
        public bool AccessDeniedWhenMissing;
        public bool CopyFails;
        public List<string> HeadedKeys { get; } = [];
        public Dictionary<string, (long Length, string Mime)> Objects { get; } = [];
        public List<string> DeletedKeys { get; } = [];
        public Task<(string Url, DateTime ExpiresAt, IReadOnlyDictionary<string, string> Headers)>
            SignPutAsync(string key, string mime, CancellationToken ct) =>
            Task.FromResult<(string, DateTime, IReadOnlyDictionary<string, string>)>(
                ("https://local.invalid/put", DateTime.UtcNow.AddMinutes(4),
                    new Dictionary<string, string> { ["Content-Type"] = mime }));
        public Task<(long Length, string Mime)?> HeadAsync(string key, CancellationToken ct)
        {
            HeadedKeys.Add(key);
            if (AccessDeniedWhenMissing && !Objects.ContainsKey(key))
                throw new AmazonS3Exception("Access denied")
                {
                    ErrorCode = "AccessDenied",
                    StatusCode = HttpStatusCode.Forbidden
                };
            return Task.FromResult(Objects.TryGetValue(key, out var value)
                ? ((long Length, string Mime)?)value : Head);
        }
        public Task<MediaReadUrl> SignGetAsync(string key, CancellationToken ct) =>
            Task.FromResult(new MediaReadUrl("https://local.invalid/get", DateTime.UtcNow.AddMinutes(4)));
        public Task CopyAsync(string sourceKey, string targetKey, string mime, CancellationToken ct)
        {
            Copies++;
            if (CopyFails)
                throw new AmazonS3Exception("copy failed") { ErrorCode = "InternalError" };
            if (Objects.TryGetValue(sourceKey, out var value)) Objects[targetKey] = value;
            return Task.CompletedTask;
        }
        public Task DeleteAsync(string key, CancellationToken ct)
        {
            Deletes++;
            DeletedKeys.Add(key);
            Objects.Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class Moderator : IImageModerator
    {
        public bool Safe = true;
        public int Calls;
        public Task<bool> IsSafeAsync(string key, string mime, long size, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(Safe);
        }
    }

    private sealed class InvalidImageModerator : IImageModerator
    {
        public Task<bool> IsSafeAsync(string key, string mime, long size, CancellationToken ct) =>
            throw new InvalidMediaImageException();
    }

    private sealed class DelayedModerator : IImageModerator
    {
        public int Calls;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> result = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<bool> IsSafeAsync(string key, string mime, long size, CancellationToken ct)
        {
            Calls++;
            Started.SetResult();
            return result.Task;
        }
        public void Complete(bool safe) => result.SetResult(safe);
    }
}
