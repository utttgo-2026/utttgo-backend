using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using UTTTGO.Api.Modules.Identity.Application;

namespace UTTTGO.Api.Modules.Identity.Infrastructure;

public sealed class SupabaseJwtOptions
{
    public string? JwksUrl { get; set; }
    public string? Secret { get; set; }
    public string? Issuer { get; set; }
    public string? Audience { get; set; }
}

public sealed class RedisJwtGate(IServiceProvider services, IOptions<SupabaseJwtOptions> settings,
    HttpClient httpClient) : ILoginJwtGate
{
    public async Task<bool> IsValidAndNotRevokedAsync(string token, string subject,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = settings.Value;
        var jwks = !string.IsNullOrWhiteSpace(options.JwksUrl);
        var secret = !string.IsNullOrWhiteSpace(options.Secret);
        if (jwks == secret || string.IsNullOrWhiteSpace(options.Issuer) ||
            string.IsNullOrWhiteSpace(options.Audience))
            throw new ExternalDependencyUnavailableException();

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        JwtSecurityToken untrusted;
        try
        {
            untrusted = handler.ReadJwtToken(token);
        }
        catch (Exception exception) when (exception is ArgumentException or SecurityTokenException)
        {
            return false;
        }

        if (untrusted.Header.Alg != (jwks ? SecurityAlgorithms.RsaSha256 : SecurityAlgorithms.HmacSha256) &&
            !(jwks && untrusted.Header.Alg == SecurityAlgorithms.EcdsaSha256)) return false;
        if (RevocationId(untrusted) is null ||
            !untrusted.Payload.ContainsKey("exp") || string.IsNullOrWhiteSpace(untrusted.Payload.Sub)) return false;

        IEnumerable<SecurityKey> signingKeys;
        if (jwks)
        {
            if (!Uri.TryCreate(options.JwksUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                throw new ExternalDependencyUnavailableException();
            // A missing/unreachable JWKS must never fall back to an unverified or locally signed token.
            var document = await httpClient.GetStringAsync(uri, cancellationToken);
            signingKeys = new JsonWebKeySet(document).GetSigningKeys()
                .Where(key => !string.IsNullOrEmpty(untrusted.Header.Kid) && key.KeyId == untrusted.Header.Kid)
                .ToArray();
        }
        else
        {
            var bytes = Encoding.UTF8.GetBytes(options.Secret!);
            if (bytes.Length < 32) throw new ExternalDependencyUnavailableException();
            signingKeys = [new SymmetricSecurityKey(bytes)];
        }

        try
        {
            var parameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = signingKeys,
                RequireSignedTokens = true,
                ValidateIssuer = true,
                ValidIssuer = options.Issuer,
                ValidateAudience = true,
                ValidAudience = options.Audience,
                RequireExpirationTime = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                ValidAlgorithms = jwks
                    ? [SecurityAlgorithms.RsaSha256, SecurityAlgorithms.EcdsaSha256]
                    : [SecurityAlgorithms.HmacSha256]
            };
            var principal = handler.ValidateToken(token, parameters, out var validated);
            if (validated is not JwtSecurityToken signed ||
                !string.Equals(signed.Payload.Sub, subject, StringComparison.Ordinal) ||
                !string.Equals(principal.FindFirst("sub")?.Value, subject, StringComparison.Ordinal) ||
                RevocationId(signed) is not { } revocationId) return false;
            return !await services.GetRequiredService<IConnectionMultiplexer>().GetDatabase()
                .KeyExistsAsync($"revoked:{revocationId}");
        }
        catch (SecurityTokenException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static string? RevocationId(JwtSecurityToken token)
    {
        if (!string.IsNullOrWhiteSpace(token.Payload.Jti)) return token.Payload.Jti;
        return token.Payload.TryGetValue("session_id", out var value) &&
            value is string sessionId && !string.IsNullOrWhiteSpace(sessionId) ? sessionId : null;
    }
}
