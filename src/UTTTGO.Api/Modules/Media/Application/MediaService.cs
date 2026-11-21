using MongoDB.Bson;
using UTTTGO.Api.Modules.Media.Contracts;

namespace UTTTGO.Api.Modules.Media.Application;

public sealed class MediaService(IMediaRepository repository, IMediaObjectStore objects,
    IImageModerator moderator) : IProductMediaAccess
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
            var head = await objects.HeadAsync(asset.ObjectKey, ct);
            if (head is null || head.Value.Length != asset.Size || head.Value.Length > MediaImagePolicy.MaxBytes ||
                !string.Equals(head.Value.Mime, asset.MimeType, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Uploaded object does not match its intent.");
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
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                await repository.TransitionAsync(asset.Id, owner, asset.Version,
                    MediaStatus.PendingModeration, MediaStatus.ProcessingFailed, ct);
            }
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            await repository.TransitionAsync(asset.Id, owner, asset.Version,
                MediaStatus.PendingModeration, MediaStatus.ProcessingFailed, ct);
        }
        return await CurrentAsync(id, owner, ct);
    }

    private async Task<ConfirmResponse?> CurrentAsync(string id, string owner, CancellationToken ct)
    {
        var record = await ReadOwnedAsync(id, owner, ct);
        return record is null ? null : new(record.Id, record.Status);
    }

    public async Task<bool> IsApprovedForOwnerAsync(string id, string owner, CancellationToken ct) =>
        await ReadOwnedAsync(id, owner, ct) is { Status: MediaStatus.Approved };

    public async Task<MediaReadUrl?> GetApprovedReadUrlAsync(string id, CancellationToken ct)
    {
        var record = await repository.FindAsync(id, ct);
        return record is { Status: MediaStatus.Approved }
            ? await objects.SignGetAsync(record.ObjectKey, ct) : null;
    }
}
