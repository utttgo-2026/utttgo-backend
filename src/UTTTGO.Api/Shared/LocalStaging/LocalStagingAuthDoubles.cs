using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using UTTTGO.Api.Modules.Identity.Application;

namespace UTTTGO.Api.Shared.LocalStaging;

public sealed class LocalStagingAuthDoubles : ISupabaseRegistration, ISupabaseLogin,
    IValidatedJwtSubjectReader, ILoginJwtGate, ISupabaseSessionIdentity, ISupabaseSessionRevoker
{
    public const string Issuer = "https://local-staging.supabase.invalid";
    public const string Audience = "local-staging";
    private const string SigningKey = "local-staging-only-signing-key-32-bytes";
    private readonly IConnectionMultiplexer redis;
    private readonly Dictionary<string, string> subjectsByEmail = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> emailsBySubject = new(StringComparer.Ordinal);

    public LocalStagingAuthDoubles(IConnectionMultiplexer redis) => this.redis = redis;

    public Task<SupabaseSignUpResult> SignUpAsync(string email, string password,
        CancellationToken cancellationToken)
    {
        if (subjectsByEmail.ContainsKey(email))
            return Task.FromResult(new SupabaseSignUpResult(true, null, null));
        var subject = "local-subject-" + Guid.NewGuid().ToString("N");
        subjectsByEmail[email] = subject;
        emailsBySubject[subject] = email;
        return Task.FromResult(new SupabaseSignUpResult(false, subject, email));
    }

    public Task<SupabaseLoginResult> AuthenticateAndCheckConfirmationAsync(string email, string password,
        CancellationToken cancellationToken)
    {
        if (!subjectsByEmail.TryGetValue(email, out var subject))
            return Task.FromResult(new SupabaseLoginResult(false, false, null, null, null, 0));
        return Task.FromResult(new SupabaseLoginResult(true, true, subject, email, CreateToken(subject), 3600));
    }

    public Task<string?> ReadValidatedSubjectAsync(string token, CancellationToken cancellationToken) =>
        Task.FromResult(Validate(token)?.FindFirstValue(ClaimTypes.NameIdentifier));

    public async Task<bool> IsValidAndNotRevokedAsync(string token, string subject,
        CancellationToken cancellationToken)
    {
        var principal = Validate(token);
        if (principal?.FindFirstValue(ClaimTypes.NameIdentifier) != subject) return false;
        var jti = principal.FindFirstValue(JwtRegisteredClaimNames.Jti);
        return jti is not null && !await redis.GetDatabase().KeyExistsAsync($"revoked:{jti}");
    }

    public Task<ConfirmedSessionIdentity?> FindBySubjectAsync(string subject,
        CancellationToken cancellationToken) => Task.FromResult(emailsBySubject.TryGetValue(subject, out var email)
            ? new ConfirmedSessionIdentity(subject, email, true) : null);

    public Task RevokeGloballyAsync(string accessToken, CancellationToken cancellationToken) => Task.CompletedTask;

    private static string CreateToken(string subject)
    {
        var now = DateTimeOffset.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim(JwtRegisteredClaimNames.Sub, subject),
                new Claim(ClaimTypes.NameIdentifier, subject),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                new Claim("auth_time", now.ToUnixTimeSeconds().ToString())]),
            Issuer = Issuer,
            Audience = Audience,
            NotBefore = now.UtcDateTime,
            IssuedAt = now.UtcDateTime,
            Expires = now.AddHours(1).UtcDateTime,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                SecurityAlgorithms.HmacSha256)
        };
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityTokenHandler().CreateToken(descriptor));
    }

    private static ClaimsPrincipal? Validate(string token)
    {
        try
        {
            return new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                ValidateIssuer = true,
                ValidIssuer = Issuer,
                ValidateAudience = true,
                ValidAudience = Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out _);
        }
        catch (SecurityTokenException) { return null; }
        catch (ArgumentException) { return null; }
    }
}
