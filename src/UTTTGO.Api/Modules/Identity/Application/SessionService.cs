using UTTTGO.Api.Modules.Identity.Contracts;

namespace UTTTGO.Api.Modules.Identity.Application;

public interface ISessionService
{
    Task<SessionResult> GetSessionAsync(string token, CancellationToken cancellationToken);
    Task<SessionAuthorization> AuthenticateAsync(string token, CancellationToken cancellationToken);
}

public enum SessionOutcome { Success, Unauthorized, ProfileMissing, Unavailable }

public sealed record SessionResult(SessionOutcome Outcome, SessionResponse? Response = null);
public sealed record SessionAuthorization(SessionOutcome Outcome, LocalLoginProfile? Profile = null);

public sealed class SessionService(
    IValidatedJwtSubjectReader subjects,
    ILoginJwtGate jwtGate,
    ISupabaseSessionIdentity supabase,
    ILoginProfileRepository profiles) : ISessionService
{
    public async Task<SessionResult> GetSessionAsync(string token, CancellationToken cancellationToken)
    {
        var authorization = await AuthenticateAsync(token, cancellationToken);
        var profile = authorization.Profile;
        return profile is null ? new(authorization.Outcome) : new(authorization.Outcome,
            new SessionResponse(profile.Id, profile.Email, profile.DisplayName!,
                profile.Roles.ToArray(), profile.AccountStatus));
    }

    public async Task<SessionAuthorization> AuthenticateAsync(string token, CancellationToken cancellationToken)
    {
        try
        {
            var subject = await subjects.ReadValidatedSubjectAsync(token, cancellationToken);
            if (string.IsNullOrWhiteSpace(subject)) return new(SessionOutcome.Unauthorized);

            // Reuse the API-002 gate: it must also check token/sub binding and Redis revocation.
            if (!await jwtGate.IsValidAndNotRevokedAsync(token, subject, cancellationToken))
                return new(SessionOutcome.Unauthorized);

            var identity = await supabase.FindBySubjectAsync(subject, cancellationToken);
            if (identity is null || !identity.EmailConfirmed ||
                !string.Equals(identity.Subject, subject, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(identity.Email))
                return new(SessionOutcome.Unauthorized);

            var profile = await profiles.FindBySubjectAsync(subject, cancellationToken);
            if (profile is null) return new(SessionOutcome.ProfileMissing);
            if (profile.AccountStatus != "Active" || string.IsNullOrWhiteSpace(profile.Id) ||
                string.IsNullOrWhiteSpace(profile.DisplayName) ||
                !string.Equals(profile.SupabaseUserId, subject, StringComparison.Ordinal) ||
                !string.Equals(profile.Email, identity.Email, StringComparison.OrdinalIgnoreCase) ||
                profile.Roles is null || profile.Roles.Count == 0 ||
                profile.Roles.Any(string.IsNullOrWhiteSpace))
                return new(SessionOutcome.Unauthorized);

            return new(SessionOutcome.Success, profile);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new(SessionOutcome.Unavailable);
        }
    }
}
