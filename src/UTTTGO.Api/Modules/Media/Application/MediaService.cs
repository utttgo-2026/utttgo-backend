using MongoDB.Bson;
using UTTTGO.Api.Modules.Media.Contracts;

namespace UTTTGO.Api.Modules.Media.Application;

public sealed class MediaService(IMediaRepository repository, IMediaObjectStore objects,
    IImageModerator moderator, ILogger<MediaService>? logger = null,
    IHttpContextAccessor? httpContextAccessor = null) : IProductMediaAccess
{
    public async Task<PresignedUrlResponse> StartAsync(string owner, PresignedUrlRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(owner) || request.Size is <= 0 or > MediaImagePolicy.MaxBytes ||
            request.MimeType is not ("image/jpeg" or "image/png" or "image/webp") ||
            string.IsNullOrWhiteSpace(request.FileName) || request.FileName.Length > 200 ||
            !string.Equals(Path.GetExtension(request.FileName), request.MimeType switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                _ => ".webp"
            }, StringComparison.OrdinalIgnoreCase) &&
            !(request.MimeType == "image/jpeg" &&
                string.Equals(Path.GetExtension(request.FileName), ".jpeg", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Invalid image metadata.");
        var id = ObjectId.GenerateNewId().ToString();
        var key = $"staging/{owner}/{id}";
        var signed = await objects.SignPutAsync(key, request.MimeType, cancellationToken);
        if (signed.ExpiresAt > DateTime.UtcNow.AddMinutes(5) ||
            !signed.Headers.TryGetValue("Content-Type", out var type) || type != request.MimeType)
            throw new InvalidOperationException("Invalid upload signature.");
        await repository.AddAsync(new MediaRecord(id, owner, key, request.MimeType, request.Size,
            MediaStatus.PendingUpload, DateTime.UtcNow), cancellationToken);
        return new(id, signed.Url, signed.ExpiresAt, signed.Headers);
    }

    public async Task<MediaRecord?> ReadOwnedAsync(string id, string owner, CancellationToken ct)
    {
        var record = await repository.FindAsync(id, ct);
        return record?.OwnerUserId == owner ? record : null;
    }

    public async Task<ConfirmResponse?> ConfirmAsync(string id, string owner, CancellationToken ct)
    {
        var asset = await ReadOwnedAsync(id, owner, ct);
        if (asset is null) return null;
        if (asset.Status is MediaStatus.Approved or MediaStatus.Rejected or MediaStatus.ProcessingFailed)
            return new(asset.Id, asset.Status);
        if (asset.Status == MediaStatus.PendingModeration)
        {
            // Only the first CAS claimant moderates. A timed-out or crashed request cannot
            // grant another claimant approval: stale work becomes terminal fail-closed.
            if (asset.ModerationStartedAt is { } started &&
                started <= DateTime.UtcNow.AddMinutes(-2))
                await repository.TransitionAsync(asset.Id, owner, asset.Version,
                    MediaStatus.PendingModeration, MediaStatus.ProcessingFailed, ct);
            return await CurrentAsync(id, owner, ct);
        }
        if (asset.Status == MediaStatus.PendingUpload)
        {
            (long Length, string Mime)? head;
            try
            {
                head = await objects.HeadAsync(asset.ObjectKey, ct);
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                return await FailAsync(asset, MediaCompletionFailure.StorageUnavailable,
                    observedSize: null, observedMime: null, ct);
            }

            if (head is null)
                return await FailAsync(asset, MediaCompletionFailure.ObjectNotUploaded,
                    observedSize: null, observedMime: null, ct);
            if (head.Value.Length != asset.Size || head.Value.Length > MediaImagePolicy.MaxBytes)
                return await FailAsync(asset, MediaCompletionFailure.SizeMismatch,
                    head.Value.Length, head.Value.Mime, ct);
            if (!string.Equals(head.Value.Mime, asset.MimeType, StringComparison.OrdinalIgnoreCase))
                return await FailAsync(asset, MediaCompletionFailure.MimeMismatch,
                    head.Value.Length, head.Value.Mime, ct);
            if (!await repository.TransitionAsync(asset.Id, owner, asset.Version,
                    MediaStatus.PendingUpload, MediaStatus.PendingModeration, ct))
                return await CurrentAsync(id, owner, ct);
            asset = asset with
            {
                Status = MediaStatus.PendingModeration,
                Version = asset.Version + 1,
                ModerationStartedAt = DateTime.UtcNow
            };
        }
        try
        {
            var safe = await moderator.IsSafeAsync(asset.ObjectKey, asset.MimeType, asset.Size, ct);
            if (!safe)
            {
                await objects.DeleteAsync(asset.ObjectKey, ct);
                await repository.TransitionAsync(asset.Id, owner, asset.Version,
                    MediaStatus.PendingModeration, MediaStatus.Rejected, ct);
            }
            else
                await repository.TransitionAsync(asset.Id, owner, asset.Version,
                    MediaStatus.PendingModeration, MediaStatus.Approved, ct);
        }
        catch (InvalidMediaImageException) when (!ct.IsCancellationRequested)
        {
            try
            {
                await objects.DeleteAsync(asset.ObjectKey, ct);
                await repository.TransitionAsync(asset.Id, owner, asset.Version,
                    MediaStatus.PendingModeration, MediaStatus.Rejected, ct);
                return await FailAsync(asset, MediaCompletionFailure.ContentInvalid,
                    asset.Size, asset.MimeType, ct);
            }
            catch (MediaCompletionException)
            {
                throw;
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                await repository.TransitionAsync(asset.Id, owner, asset.Version,
                    MediaStatus.PendingModeration, MediaStatus.ProcessingFailed, ct);
                return await FailAsync(asset, MediaCompletionFailure.StorageUnavailable,
                    asset.Size, asset.MimeType, ct);
            }
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            await repository.TransitionAsync(asset.Id, owner, asset.Version,
                MediaStatus.PendingModeration, MediaStatus.ProcessingFailed, ct);
        }
        return await CurrentAsync(id, owner, ct);
    }

    private Task<ConfirmResponse?> FailAsync(MediaRecord asset,
        MediaCompletionFailure failure, long? observedSize, string? observedMime,
        CancellationToken ct)
    {
        logger?.LogWarning(
            "Media completion failed. AssetId={AssetId} Cause={Cause} DeclaredSize={DeclaredSize} ObservedSize={ObservedSize} DeclaredMime={DeclaredMime} ObservedMime={ObservedMime} CorrelationId={CorrelationId}",
            asset.Id, failure, asset.Size, observedSize, asset.MimeType, observedMime,
            httpContextAccessor?.HttpContext?.TraceIdentifier);
        throw new MediaCompletionException(failure);
    }

    private async Task<ConfirmResponse?> CurrentAsync(string id, string owner, CancellationToken ct)
    {
        var record = await ReadOwnedAsync(id, owner, ct);
        return record is null ? null : new(record.Id, record.Status);
    }

    public async Task<bool> IsApprovedForOwnerAsync(string id, string owner, CancellationToken ct) =>
        await ReadOwnedAsync(id, owner, ct) is { Status: MediaStatus.Approved };

    public async Task<bool> PromoteApprovedAsync(string id, string owner, CancellationToken ct)
    {
        var asset = await ReadOwnedAsync(id, owner, ct);
        if (asset is not { Status: MediaStatus.Approved }) return false;
        var targetKey = $"approved/{owner}/{id}";
        if (asset.ObjectKey == targetKey) return true;
        if (!asset.ObjectKey.Equals($"staging/{owner}/{id}", StringComparison.Ordinal)) return false;

        var destination = await objects.HeadAsync(targetKey, ct);
        var createdDestination = destination is null;
        if (destination is not null &&
            (destination.Value.Length != asset.Size ||
             !string.Equals(destination.Value.Mime, asset.MimeType, StringComparison.OrdinalIgnoreCase)))
            return false;

        var mongoPromoted = false;
        try
        {
            if (createdDestination)
            {
                var source = await objects.HeadAsync(asset.ObjectKey, ct);
                if (source is null || source.Value.Length != asset.Size ||
                    !string.Equals(source.Value.Mime, asset.MimeType, StringComparison.OrdinalIgnoreCase))
                    return false;
                await objects.CopyAsync(asset.ObjectKey, targetKey, asset.MimeType, ct);
                var copied = await objects.HeadAsync(targetKey, ct);
                if (copied is null || copied.Value.Length != asset.Size ||
                    !string.Equals(copied.Value.Mime, asset.MimeType, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Approved media copy verification failed.");
            }

            if (!await repository.PromoteAsync(id, owner, asset.Version, asset.ObjectKey, targetKey, ct))
            {
                var current = await ReadOwnedAsync(id, owner, ct);
                if (current is not { Status: MediaStatus.Approved } || current.ObjectKey != targetKey)
                {
                    if (createdDestination) await objects.DeleteAsync(targetKey, ct);
                    return false;
                }
            }
            mongoPromoted = true;

            await objects.DeleteAsync(asset.ObjectKey, ct);
            return true;
        }
        catch
        {
            if (createdDestination && !mongoPromoted)
            {
                try { await objects.DeleteAsync(targetKey, ct); }
                catch (Exception) when (!ct.IsCancellationRequested) { }
            }
            throw;
        }
    }

    public async Task<MediaReadUrl?> GetApprovedReadUrlAsync(string id, CancellationToken ct)
    {
        var record = await repository.FindAsync(id, ct);
        return record is { Status: MediaStatus.Approved }
            ? await objects.SignGetAsync(record.ObjectKey, ct) : null;
    }

    public async Task<IReadOnlyDictionary<string, MediaReadUrl>> GetApprovedReadUrlsAsync(
        IEnumerable<string> ids, CancellationToken ct)
    {
        var results = await Task.WhenAll(ids.Distinct(StringComparer.Ordinal)
            .Select(async id => (Id: id, Url: await GetApprovedReadUrlAsync(id, ct))));
        return results.Where(item => item.Url is not null)
            .ToDictionary(item => item.Id, item => item.Url!, StringComparer.Ordinal);
    }
}
