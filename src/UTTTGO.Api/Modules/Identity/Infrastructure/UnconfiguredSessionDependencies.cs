using UTTTGO.Api.Modules.Identity.Application;

namespace UTTTGO.Api.Modules.Identity.Infrastructure;

public sealed class UnconfiguredValidatedJwtSubjectReader : IValidatedJwtSubjectReader
{
    public Task<string?> ReadValidatedSubjectAsync(string token, CancellationToken cancellationToken) =>
        throw new ExternalDependencyUnavailableException();
}

public sealed class UnconfiguredSupabaseSessionIdentity : ISupabaseSessionIdentity
{
    public Task<ConfirmedSessionIdentity?> FindBySubjectAsync(string subject, CancellationToken cancellationToken) =>
        throw new ExternalDependencyUnavailableException();
}
