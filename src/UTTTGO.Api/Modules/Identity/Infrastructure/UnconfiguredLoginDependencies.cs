using UTTTGO.Api.Modules.Identity.Application;

namespace UTTTGO.Api.Modules.Identity.Infrastructure;

// Fail-closed placeholders. No simulated authentication is registered in the application.
public sealed class UnconfiguredSupabaseLogin : ISupabaseLogin
{
    public Task<SupabaseLoginResult> AuthenticateAndCheckConfirmationAsync(string email, string password,
        CancellationToken cancellationToken) => throw new ExternalDependencyUnavailableException();
}

public sealed class UnconfiguredLoginProfileRepository : ILoginProfileRepository
{
    public Task<LocalLoginProfile?> FindBySubjectAsync(string subject, CancellationToken cancellationToken) =>
        throw new ExternalDependencyUnavailableException();
}

public sealed class UnconfiguredLoginJwtGate : ILoginJwtGate
{
    public Task<bool> IsValidAndNotRevokedAsync(string token, string subject, CancellationToken cancellationToken) =>
        throw new ExternalDependencyUnavailableException();
}
