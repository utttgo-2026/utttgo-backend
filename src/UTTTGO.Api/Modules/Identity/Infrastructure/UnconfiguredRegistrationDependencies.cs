using UTTTGO.Api.Modules.Identity.Application;

namespace UTTTGO.Api.Modules.Identity.Infrastructure;

// These are fail-closed placeholders, never fake authentication or persistence.
public sealed class UnconfiguredAuthRateLimitGate : IAuthRateLimitGate
{
    public Task<bool> IsAllowedAsync(CancellationToken cancellationToken) =>
        throw new ExternalDependencyUnavailableException();
}

public sealed class UnconfiguredSupabaseRegistration : ISupabaseRegistration
{
    public Task<SupabaseSignUpResult> SignUpAsync(string email, string password, CancellationToken cancellationToken) =>
        throw new ExternalDependencyUnavailableException();
}

public sealed class UnconfiguredUserRegistrationRepository : IUserRegistrationRepository
{
    public Task EnsureAvailableAsync(CancellationToken cancellationToken) =>
        throw new ExternalDependencyUnavailableException();

    public Task SavePendingBuyerAsync(PendingBuyerProfile profile, CancellationToken cancellationToken) =>
        throw new ExternalDependencyUnavailableException();
}
