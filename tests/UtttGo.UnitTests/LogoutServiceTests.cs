using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using UTTTGO.Api.Modules.Identity.Application;
using Xunit;

namespace UtttGo.UnitTests;

public sealed class LogoutServiceTests
{
    private const string Secret = "a-test-only-signing-key-that-is-at-least-32-bytes-long";

    [Fact]
    public async Task ValidTokenIsRevokedUntilItsExpiration()
    {
        var store = new Store(true);
        var service = Service(store);

        var result = await service.LogoutAsync(TokenWithSession(null, "session-1", DateTime.UtcNow.AddMinutes(5)), default);

        result.Outcome.Should().Be(LogoutOutcome.Success);
        store.Jti.Should().Be("session-1");
        store.Ttl.Should().BeGreaterThan(TimeSpan.FromMinutes(4));
        store.Ttl.Should().BeLessThan(TimeSpan.FromMinutes(5));
    }

    [Theory]
    [InlineData("invalid-token")]
    [InlineData("")]
    public async Task InvalidTokenReturnsUnauthorizedWithoutWriting(string token)
    {
        var store = new Store(true);
        var result = await Service(store).LogoutAsync(token, default);

        result.Outcome.Should().Be(LogoutOutcome.Unauthorized);
        store.Calls.Should().Be(0);
    }

    [Fact]
    public async Task ExpiredTokenReturnsUnauthorized()
    {
        var store = new Store(true);
        var result = await Service(store)
            .LogoutAsync(Token("expired", DateTime.UtcNow.AddMinutes(-1)), default);

        result.Outcome.Should().Be(LogoutOutcome.Unauthorized);
        store.Calls.Should().Be(0);
    }

    [Fact]
    public async Task TokenWithoutSessionIdOrJtiUsesSha256Identifier()
    {
        var store = new Store(true);
        var result = await Service(store)
            .LogoutAsync(TokenWithoutIdentifiers(DateTime.UtcNow.AddMinutes(5)), default);

        result.Outcome.Should().Be(LogoutOutcome.Success);
        store.Calls.Should().Be(1);
    }

    [Fact]
    public async Task SupabaseSessionIdIsPreferredToJti()
    {
        var store = new Store(true);
        var result = await Service(store)
            .LogoutAsync(TokenWithSession("jwt-id", "session-id", DateTime.UtcNow.AddMinutes(5)), default);

        result.Outcome.Should().Be(LogoutOutcome.Success);
        store.Jti.Should().Be("session-id");
    }

    [Fact]
    public async Task JtiIsUsedWhenSessionIdIsAbsent()
    {
        var store = new Store(true);
        var token = Token("jti-fallback", DateTime.UtcNow.AddMinutes(5));

        var result = await Service(store).LogoutAsync(token, default);

        result.Outcome.Should().Be(LogoutOutcome.Success);
        store.Jti.Should().Be("jti-fallback");
    }

    [Fact]
    public async Task AlreadyRevokedTokenReturnsUnauthorized()
    {
        var store = new Store(false);
        var result = await Service(store)
            .LogoutAsync(Token("revoked", DateTime.UtcNow.AddMinutes(5)), default);

        result.Outcome.Should().Be(LogoutOutcome.Unauthorized);
        store.Calls.Should().Be(1);
    }

    [Fact]
    public async Task RedisFailureReturnsUnavailable()
    {
        var store = new Store(true) { Failure = true };
        var revoker = new Revoker();
        var result = await Service(store, revoker)
            .LogoutAsync(Token("redis-failure", DateTime.UtcNow.AddMinutes(5)), default);

        result.Outcome.Should().Be(LogoutOutcome.Unavailable);
        revoker.Calls.Should().Be(0);
    }

    private static LogoutService Service(Store store, Revoker? revoker = null) =>
        new(new Gate(true), store, revoker ?? new Revoker());

    private static string Token(string jti, DateTime expires)
    {
        var jwt = new JwtSecurityToken("https://example.supabase.co/auth/v1", "authenticated",
            [new Claim("sub", "subject-1"), new Claim(JwtRegisteredClaimNames.Jti, jti)],
            expires.AddMinutes(-10), expires,
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private static string TokenWithoutIdentifiers(DateTime expires)
    {
        var jwt = new JwtSecurityToken("https://example.supabase.co/auth/v1", "authenticated",
            [new Claim("sub", "subject-1")], expires.AddMinutes(-10), expires,
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private static string TokenWithSession(string? jti, string sessionId, DateTime expires)
    {
        var claims = new List<Claim> { new("sub", "subject-1"), new("session_id", sessionId) };
        if (jti is not null) claims.Add(new Claim(JwtRegisteredClaimNames.Jti, jti));
        var jwt = new JwtSecurityToken("https://example.supabase.co/auth/v1", "authenticated",
            claims, expires.AddMinutes(-10), expires,
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private sealed class Gate(bool allowed) : ILoginJwtGate
    {
        public Task<bool> IsValidAndNotRevokedAsync(string token, string subject,
            CancellationToken cancellationToken) => Task.FromResult(allowed);
    }

    private sealed class Store(bool result) : IJwtRevocationStore
    {
        public string? Jti { get; private set; }
        public TimeSpan Ttl { get; private set; }
        public int Calls { get; private set; }
        public bool Failure { get; init; }

        public Task<bool> TryRevokeAsync(string jti, TimeSpan ttl, CancellationToken cancellationToken)
        {
            Calls++;
            Jti = jti;
            Ttl = ttl;
            if (Failure) throw new ExternalDependencyUnavailableException();
            return Task.FromResult(result);
        }
    }

    private sealed class Revoker : ISupabaseSessionRevoker
    {
        public int Calls { get; private set; }

        public Task RevokeGloballyAsync(string accessToken, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }
}
