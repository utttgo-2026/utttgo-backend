namespace UTTTGO.Api.Modules.Identity.Application;

// An adapter must check current confirmation with Supabase Auth server, not a client claim.
public interface ISupabaseLogin
{
    Task<SupabaseLoginResult> AuthenticateAndCheckConfirmationAsync(string email, string password,
        CancellationToken cancellationToken);
}

public sealed record SupabaseLoginResult(
    bool CredentialsValid, bool EmailConfirmed, string? Subject, string? Email,
    string? AccessToken, long ExpiresIn);

public interface ILoginProfileRepository
{
    Task<LocalLoginProfile?> FindBySubjectAsync(string subject, CancellationToken cancellationToken);
}

public sealed record LocalLoginProfile(string Id, string SupabaseUserId, string Email,
    string AccountStatus, IReadOnlyList<string> Roles, string? DisplayName = null,
    string? Phone = null, string? AvatarMediaAssetId = null, long Version = 0);

// A real adapter must validate JWT signature, issuer, audience, expiry, jti and Redis blacklist.
public interface ILoginJwtGate
{
    Task<bool> IsValidAndNotRevokedAsync(string token, string subject, CancellationToken cancellationToken);
}
