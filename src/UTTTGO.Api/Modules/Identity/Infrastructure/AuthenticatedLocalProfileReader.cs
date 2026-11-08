using UTTTGO.Api.Modules.Identity.Application;

namespace UTTTGO.Api.Modules.Identity.Infrastructure;

public sealed class AuthenticatedLocalProfileReader(IValidatedJwtSubjectReader subjects,
    ILoginJwtGate jwtGate, ISupabaseSessionIdentity supabase, ILoginProfileRepository profiles)
    : IAuthenticatedLocalProfileReader
{
    public async Task<AuthenticatedLocalProfileResult> ReadAsync(string token,
        CancellationToken cancellationToken)
    {
        try
        {
            var subject = await subjects.ReadValidatedSubjectAsync(token, cancellationToken);
            if (string.IsNullOrWhiteSpace(subject))
                return new(AuthenticatedLocalProfileOutcome.Unauthorized);

            // Same gate as API-002/API-004: signature, issuer, audience, expiry and Redis revocation.
            if (!await jwtGate.IsValidAndNotRevokedAsync(token, subject, cancellationToken))
                return new(AuthenticatedLocalProfileOutcome.Unauthorized);

            var identity = await supabase.FindBySubjectAsync(subject, cancellationToken);
            if (identity is null || !identity.EmailConfirmed ||
                !string.Equals(identity.Subject, subject, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(identity.Email))
                return new(AuthenticatedLocalProfileOutcome.Unauthorized);

            var profile = await profiles.FindBySubjectAsync(subject, cancellationToken);
            if (profile is null) return new(AuthenticatedLocalProfileOutcome.ProfileMissing);
            if (string.IsNullOrWhiteSpace(profile.Id) ||
                !string.Equals(profile.SupabaseUserId, subject, StringComparison.Ordinal) ||
                profile.Roles is null || profile.Roles.Count == 0 ||
                profile.Roles.Any(string.IsNullOrWhiteSpace))
                return new(AuthenticatedLocalProfileOutcome.Unauthorized);

            // The account status is reported as stored: only the owner module decides what it means.
            return new(AuthenticatedLocalProfileOutcome.Success, profile);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new(AuthenticatedLocalProfileOutcome.Unavailable);
        }
    }
}
