using UTTTGO.Api.Modules.Identity.Application;

namespace UTTTGO.Api.Modules.Identity.Infrastructure;

public sealed class UnconfiguredProfileUpdateRepository : IProfileUpdateRepository
{
    public Task<ProfileUpdateResult> UpdateOwnAsync(string subject, string localId, long expectedVersion,
        string? displayName, bool changeDisplayName, string? phone, bool changePhone,
        CancellationToken cancellationToken) => throw new ExternalDependencyUnavailableException();
}
