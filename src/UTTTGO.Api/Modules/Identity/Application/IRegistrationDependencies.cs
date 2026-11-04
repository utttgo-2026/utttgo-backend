namespace UTTTGO.Api.Modules.Identity.Application;

public interface IAuthRateLimitGate
{
    Task<bool> IsAllowedAsync(CancellationToken cancellationToken);
}

public interface ISupabaseRegistration
{
    Task<SupabaseSignUpResult> SignUpAsync(string email, string password, CancellationToken cancellationToken);
}

public sealed record SupabaseSignUpResult(bool AlreadyExists, string? Subject, string? Email);

public sealed record PendingBuyerProfile(string SupabaseUserId, string Email, string DisplayName)
{
    public string Role => "Buyer";
    public string Status => "PendingEmailVerification";
}

public interface IUserRegistrationRepository
{
    Task EnsureAvailableAsync(CancellationToken cancellationToken);
    Task SavePendingBuyerAsync(PendingBuyerProfile profile, CancellationToken cancellationToken);
}

public sealed class ExternalDependencyUnavailableException : Exception
{
    public ExternalDependencyUnavailableException() : base("Registration dependency unavailable.") { }
}
