namespace UTTTGO.Api.Modules.Identity.Application;

public interface IRegistrationService
{
    Task<RegistrationOutcome> RegisterAsync(string email, string password, string displayName,
        CancellationToken cancellationToken);
}

public enum RegistrationOutcome { Accepted, RateLimited, Unavailable }

public sealed class RegistrationService(
    IAuthRateLimitGate rateLimit,
    ISupabaseRegistration supabase,
    IUserRegistrationRepository users) : IRegistrationService
{
    public async Task<RegistrationOutcome> RegisterAsync(string email, string password, string displayName,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!await rateLimit.IsAllowedAsync(cancellationToken)) return RegistrationOutcome.RateLimited;

            // Check shared storage before both new and existing account branches to fail closed.
            await users.EnsureAvailableAsync(cancellationToken);
            var signUp = await supabase.SignUpAsync(email, password, cancellationToken);
            if (signUp.AlreadyExists) return RegistrationOutcome.Accepted;

            if (string.IsNullOrWhiteSpace(signUp.Subject) ||
                !string.Equals(signUp.Email, email, StringComparison.OrdinalIgnoreCase))
                return RegistrationOutcome.Unavailable;

            await users.SavePendingBuyerAsync(new PendingBuyerProfile(signUp.Subject, email, displayName),
                cancellationToken);
            return RegistrationOutcome.Accepted;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Never disclose adapter exceptions or accidentally acknowledge a partial registration.
            return RegistrationOutcome.Unavailable;
        }
    }
}
