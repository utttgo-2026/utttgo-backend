using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using UTTTGO.Api.Modules.Identity.Infrastructure;
using Xunit;

namespace UtttGo.UnitTests;

public sealed class RedisJwtGateTests
{
    private const string Secret = "a-test-only-signing-key-that-is-at-least-32-bytes-long";

    [Theory]
    [InlineData("jwt-id", "session-id", "revoked:jwt-id")]
    [InlineData(null, "session-id", "revoked:session-id")]
    public async Task ValidJwtChecksPreferredRevocationIdentifier(string? jti, string? session,
        string expectedKey)
    {
        var database = DispatchProxy.Create<IDatabase, DatabaseProxy>();
        var connection = DispatchProxy.Create<IConnectionMultiplexer, MultiplexerProxy>();
        ((MultiplexerProxy)connection).Database = database;
        using var services = new ServiceCollection().AddSingleton(connection).BuildServiceProvider();
        var gate = CreateGate(services);

        (await gate.IsValidAndNotRevokedAsync(Token(jti, session), "sub-1", default)).Should().BeTrue();
        ((DatabaseProxy)database).CheckedKey.Should().Be(expectedKey);
    }

    [Fact]
    public async Task JwtWithoutJtiOrSessionIdIsRejectedBeforeRedis()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        (await CreateGate(services).IsValidAndNotRevokedAsync(Token(null, null), "sub-1", default))
            .Should().BeFalse();
    }

    private static RedisJwtGate CreateGate(IServiceProvider services) =>
        new(services, Options.Create(new SupabaseJwtOptions
        {
            Secret = Secret,
            Issuer = "https://example.supabase.co/auth/v1",
            Audience = "authenticated"
        }), new HttpClient());

    private static string Token(string? jti, string? session)
    {
        var claims = new List<Claim> { new("sub", "sub-1") };
        if (jti is not null) claims.Add(new Claim(JwtRegisteredClaimNames.Jti, jti));
        if (session is not null) claims.Add(new Claim("session_id", session));
        var jwt = new JwtSecurityToken("https://example.supabase.co/auth/v1", "authenticated", claims,
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(5),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    public class MultiplexerProxy : DispatchProxy
    {
        public IDatabase Database { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name == "GetDatabase" ? Database : throw new NotSupportedException();
    }

    public class DatabaseProxy : DispatchProxy
    {
        public string? CheckedKey { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != "KeyExistsAsync") throw new NotSupportedException();
            CheckedKey = args![0]!.ToString();
            return Task.FromResult(false);
        }
    }
}
