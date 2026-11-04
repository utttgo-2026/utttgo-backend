using UTTTGO.Api.Modules.Identity.Application;

namespace UTTTGO.Api.Modules.Identity.Infrastructure;

public sealed class UnconfiguredAdminUserListReader : IAdminUserListReader
{
    public Task<AdminUserPage> ListAsync(AdminUserListQuery query, CancellationToken cancellationToken) =>
        throw new ExternalDependencyUnavailableException();
}
