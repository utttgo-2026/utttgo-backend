namespace UTTTGO.Api.Modules.Identity.Application;

// A real adapter must validate the JWT (signature, issuer, audience, expiry) and the Redis
// blacklist before returning the local profile; it never trusts client claims.
// The local account status is returned as stored so that other modules can answer 403
// for a non-active account instead of a generic 401.
public interface IAuthenticatedLocalProfileReader
{
    Task<AuthenticatedLocalProfileResult> ReadAsync(string token, CancellationToken cancellationToken);
}

public enum AuthenticatedLocalProfileOutcome { Success, Unauthorized, ProfileMissing, Unavailable }

public sealed record AuthenticatedLocalProfileResult(AuthenticatedLocalProfileOutcome Outcome,
    LocalLoginProfile? Profile = null);
