using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;

namespace UTTTGO.Api.Modules.Identity.Application;

public sealed class LogoutService(
    ILoginJwtGate jwtGate,
    IJwtRevocationStore revocations) : ILogoutService
{
    public async Task<LogoutResult> LogoutAsync(string token, CancellationToken cancellationToken)
    {
        JwtSecurityToken parsed;
        try
        {
            parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);
        }
        catch (Exception exception) when (exception is ArgumentException or SecurityTokenException)
        {
            return new(LogoutOutcome.Unauthorized);
        }

        var subject = parsed.Payload.Sub;
        var jti = parsed.Payload.Jti;
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(jti) ||
            parsed.Payload.Expiration is not { } expiration)
            return new(LogoutOutcome.Unauthorized);

        if (!await jwtGate.IsValidAndNotRevokedAsync(token, subject, cancellationToken))
            return new(LogoutOutcome.Unauthorized);

        var ttl = DateTimeOffset.FromUnixTimeSeconds(expiration) - DateTimeOffset.UtcNow;
        if (ttl <= TimeSpan.Zero)
            return new(LogoutOutcome.Unauthorized);

        try
        {
            return await revocations.TryRevokeAsync(jti, ttl, cancellationToken)
                ? new(LogoutOutcome.Success)
                : new(LogoutOutcome.Unauthorized);
        }
        catch (ExternalDependencyUnavailableException)
        {
            return new(LogoutOutcome.Unavailable);
        }
    }
}
