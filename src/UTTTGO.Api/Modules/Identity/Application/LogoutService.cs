using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;

namespace UTTTGO.Api.Modules.Identity.Application;

public sealed class LogoutService(
    ILoginJwtGate jwtGate,
    IJwtRevocationStore revocations,
    ISupabaseSessionRevoker supabase) : ILogoutService
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
        if (string.IsNullOrWhiteSpace(subject) || parsed.Payload.Expiration is not { } expiration)
            return new(LogoutOutcome.Unauthorized);

        var revocationId = JwtRevocationIdentifier.For(parsed, token);

        if (!await jwtGate.IsValidAndNotRevokedAsync(token, subject, cancellationToken))
            return new(LogoutOutcome.Unauthorized);

        var ttl = DateTimeOffset.FromUnixTimeSeconds(expiration) - DateTimeOffset.UtcNow;
        if (ttl <= TimeSpan.Zero)
            return new(LogoutOutcome.Unauthorized);

        try
        {
            if (!await revocations.TryRevokeAsync(revocationId, ttl, cancellationToken))
                return new(LogoutOutcome.Unauthorized);
        }
        catch (ExternalDependencyUnavailableException)
        {
            return new(LogoutOutcome.Unavailable);
        }

        await supabase.RevokeGloballyAsync(token, cancellationToken);
        return new(LogoutOutcome.Success);
    }
}
