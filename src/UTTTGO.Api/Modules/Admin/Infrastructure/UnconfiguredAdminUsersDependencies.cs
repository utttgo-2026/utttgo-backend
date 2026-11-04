using UTTTGO.Api.Modules.Admin.Application;
using UTTTGO.Api.Modules.Identity.Application;

namespace UTTTGO.Api.Modules.Admin.Infrastructure;

public sealed class UnconfiguredAdminUsersRateLimitGate : IAdminUsersRateLimitGate
{
    public Task<bool> IsAllowedAsync(string actorId, CancellationToken cancellationToken) =>
        throw new ExternalDependencyUnavailableException();
}

public sealed class UnconfiguredAdminUsersAudit : IAdminUsersAudit
{
    public Task RecordAsync(string actorId, string correlationId, string result,
        CancellationToken cancellationToken) => throw new ExternalDependencyUnavailableException();
}
