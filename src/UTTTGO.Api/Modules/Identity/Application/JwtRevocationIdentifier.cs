using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;

namespace UTTTGO.Api.Modules.Identity.Application;

public static class JwtRevocationIdentifier
{
    public static string For(JwtSecurityToken token, string rawToken)
    {
        var sessionId = token.Claims.FirstOrDefault(claim => claim.Type == "session_id")?.Value;
        if (!string.IsNullOrWhiteSpace(sessionId)) return sessionId;

        if (!string.IsNullOrWhiteSpace(token.Payload.Jti)) return token.Payload.Jti;

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
    }
}
