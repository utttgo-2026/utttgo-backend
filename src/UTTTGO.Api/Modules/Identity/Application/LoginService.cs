using UTTTGO.Api.Modules.Identity.Contracts;
using UTTTGO.Api.Modules.Identity.Validators;

namespace UTTTGO.Api.Modules.Identity.Application;

public interface ILoginService
{
    Task<LoginResult> LoginAsync(string email, string password, CancellationToken cancellationToken);
}

public enum LoginOutcome { Success, InvalidCredentials, RateLimited, Unavailable }

public sealed record LoginResult(LoginOutcome Outcome, LoginResponse? Response = null);

public sealed class LoginService(
    IAuthRateLimitGate rateLimit,
    ISupabaseLogin supabase,
    ILoginProfileRepository profiles,
    ILoginJwtGate jwtGate) : ILoginService
{
    public async Task<LoginResult> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        try
        {
            if (!await rateLimit.IsAllowedAsync(cancellationToken))
                return new LoginResult(LoginOutcome.RateLimited);

            if (!RegisterRequestValidator.HasInstitutionalDomain(email))
                return new LoginResult(LoginOutcome.InvalidCredentials);

            var identity = await supabase.AuthenticateAndCheckConfirmationAsync(email, password, cancellationToken);
            if (!identity.CredentialsValid || !identity.EmailConfirmed)
                return new LoginResult(LoginOutcome.InvalidCredentials);

            if (string.IsNullOrWhiteSpace(identity.Subject) ||
                !string.Equals(identity.Email, email, StringComparison.OrdinalIgnoreCase))
                return new LoginResult(LoginOutcome.InvalidCredentials);

            // Do not auto-create or promote a profile from a login response or a JWT.
            var profile = await profiles.FindBySubjectAsync(identity.Subject, cancellationToken);
            if (profile is null || profile.AccountStatus != "Active" ||
                string.IsNullOrWhiteSpace(profile.Id) ||
                !string.Equals(profile.SupabaseUserId, identity.Subject, StringComparison.Ordinal) ||
                !string.Equals(profile.Email, identity.Email, StringComparison.OrdinalIgnoreCase) ||
                profile.Roles is null || profile.Roles.Count == 0 ||
                profile.Roles.Any(string.IsNullOrWhiteSpace))
                return new LoginResult(LoginOutcome.InvalidCredentials);

            if (string.IsNullOrWhiteSpace(identity.AccessToken) || identity.ExpiresIn <= 0)
                return new LoginResult(LoginOutcome.Unavailable);

            if (!await jwtGate.IsValidAndNotRevokedAsync(identity.AccessToken, identity.Subject,
                    cancellationToken))
                return new LoginResult(LoginOutcome.InvalidCredentials);

            return new LoginResult(LoginOutcome.Success,
                new LoginResponse(identity.AccessToken, identity.ExpiresIn, "Bearer",
                    new LoginUserResponse(profile.Id, profile.Roles.ToArray(), profile.AccountStatus)));
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Never expose credentials, provider details or partially issued sessions.
            return new LoginResult(LoginOutcome.Unavailable);
        }
    }
}
