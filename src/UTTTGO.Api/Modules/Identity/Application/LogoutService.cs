using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using UTTTGO.Api.Modules.Notifications.Application;

namespace UTTTGO.Api.Modules.Identity.Application;

public sealed class LogoutService(
    ILoginJwtGate jwtGate,
    IJwtRevocationStore revocations,
    ISupabaseSessionRevoker supabase, IDeviceTokenStore? deviceTokens = null,
    IAuthenticatedLocalProfileReader? profiles = null) : ILogoutService
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

        try
        {
            var profile = profiles is null ? null : await profiles.ReadAsync(token, cancellationToken);
            if (deviceTokens is not null && profile?.Profile is not null)
                await deviceTokens.RemoveForUserAsync(profile.Profile.Id, cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested) { }
        await supabase.RevokeGloballyAsync(token, cancellationToken);
        return new(LogoutOutcome.Success);
    }
}
