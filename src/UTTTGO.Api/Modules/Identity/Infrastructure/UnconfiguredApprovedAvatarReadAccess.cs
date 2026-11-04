using UTTTGO.Api.Modules.Identity.Application;

namespace UTTTGO.Api.Modules.Identity.Infrastructure;

// No Media integration yet: never infer approval from a stored media id or produce a public URL.
public sealed class UnconfiguredApprovedAvatarReadAccess : IApprovedAvatarReadAccess
{
    public Task<string?> GetApprovedOwnedTemporaryUrlAsync(string mediaAssetId, string ownerId,
        CancellationToken cancellationToken) => Task.FromResult<string?>(null);
}
