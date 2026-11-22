namespace UTTTGO.Api.Modules.Identity.Application;

public interface ILogoutService
{
    Task<LogoutResult> LogoutAsync(string token, CancellationToken cancellationToken);
}

public enum LogoutOutcome
{
    Success,
    Unauthorized,
    Unavailable
}

public sealed record LogoutResult(LogoutOutcome Outcome);

public interface IJwtRevocationStore
{
    Task<bool> TryRevokeAsync(string jti, TimeSpan ttl, CancellationToken cancellationToken);
}
