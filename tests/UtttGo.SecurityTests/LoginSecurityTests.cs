using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using UTTTGO.Api.Modules.Identity.Application;
using Xunit;

namespace UtttGo.SecurityTests;

public sealed class LoginSecurityTests
{
    private const string Route = "/api/v1/auth/login";
    private const string Token = "test-only-jwt-secret";
    private const string Password = "test-only-password-secret";
    private static readonly SupabaseLoginResult Verified =
        new(true, true, "sub-1", "a@uttt.edu.mx", Token, 3600);
    private static readonly LocalLoginProfile Active =
        new("user-1", "sub-1", "a@uttt.edu.mx", "Active", ["Seller"]);

    [Fact]
    public async Task WrongPasswordUnconfirmedSuspendedAndMissingProfileShareGenericPublicError()
    {
        var variants = new (SupabaseLoginResult Provider, LocalLoginProfile? Profile)[]
        {
            (Verified with { CredentialsValid = false }, Active),
            (Verified with { EmailConfirmed = false }, Active),
            (Verified, Active with { AccountStatus = "PendingEmailVerification" }),
            (Verified, Active with { AccountStatus = "Suspended" }),
            (Verified, null)
        };
        var publicErrors = new List<(HttpStatusCode, string, string)>();

        foreach (var (provider, profile) in variants)
        {
            using var factory = CreateFactory(provider, profile);
            using var client = factory.CreateClient();
            using var response = await Login(client);
            var text = await response.Content.ReadAsStringAsync();
            text.Should().NotContain(Token).And.NotContain(Password)
                .And.NotContain("PendingEmailVerification").And.NotContain("Suspended");
            using var body = JsonDocument.Parse(text);
            body.RootElement.GetProperty("correlationId").GetString().Should().NotBeNullOrWhiteSpace();
            publicErrors.Add((response.StatusCode, body.RootElement.GetProperty("code").GetString()!,
                body.RootElement.GetProperty("message").GetString()!));
        }

        publicErrors.Distinct().Should().ContainSingle();
        publicErrors[0].Item1.Should().Be(HttpStatusCode.Unauthorized);
        publicErrors[0].Item2.Should().Be("INVALID_CREDENTIALS");
        // Equality of status/code/message does not establish resistance to timing attacks.
    }

    [Fact]
    public async Task JwtValidationOrBlacklistRejectionDoesNotLeakSession()
    {
        using var factory = CreateFactory(Verified, Active, jwtAllowed: false);
        using var client = factory.CreateClient();
        using var response = await Login(client);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().NotContain(Token);
    }

    [Fact]
    public async Task SuccessfulResponseHasOnlyLocalRoleAndNeverRefreshToken()
    {
        using var factory = CreateFactory(Verified, Active);
        using var client = factory.CreateClient();
        using var response = await Login(client);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var text = await response.Content.ReadAsStringAsync();
        text.Should().Contain("Seller").And.NotContain("Admin").And.NotContain(Password)
            .And.NotContain("refreshToken");
        using var body = JsonDocument.Parse(text);
        body.RootElement.GetProperty("user").GetProperty("roles")[0].GetString().Should().Be("Seller");
    }

    [Fact]
    public async Task LoginNeverLogsPasswordOrJwtWithTestProvider()
    {
        var capture = new CapturingLoggerProvider();
        using var baseFactory = CreateFactory(Verified, Active);
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(capture)));
        using var client = factory.CreateClient();
        using var response = await Login(client);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        string.Join("\n", capture.Messages).Should().NotContain(Password).And.NotContain(Token);
    }

    private static Task<HttpResponseMessage> Login(HttpClient client) => client.PostAsJsonAsync(Route,
        new { email = "a@uttt.edu.mx", password = Password });

    private static WebApplicationFactory<Program> CreateFactory(SupabaseLoginResult provider,
        LocalLoginProfile? profile, bool jwtAllowed = true) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAuthRateLimitGate>();
            services.RemoveAll<ISupabaseLogin>();
            services.RemoveAll<ILoginProfileRepository>();
            services.RemoveAll<ILoginJwtGate>();
            services.AddSingleton<IAuthRateLimitGate>(new AllowedGate());
            services.AddSingleton<ISupabaseLogin>(new ProviderDouble(provider));
            services.AddSingleton<ILoginProfileRepository>(new ProfileDouble(profile));
            services.AddSingleton<ILoginJwtGate>(new JwtDouble(jwtAllowed));
        }));

    private sealed class AllowedGate : IAuthRateLimitGate
    {
        public Task<bool> IsAllowedAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class ProviderDouble(SupabaseLoginResult result) : ISupabaseLogin
    {
        public Task<SupabaseLoginResult> AuthenticateAndCheckConfirmationAsync(string email, string password,
            CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class ProfileDouble(LocalLoginProfile? profile) : ILoginProfileRepository
    {
        public Task<LocalLoginProfile?> FindBySubjectAsync(string subject, CancellationToken cancellationToken) =>
            Task.FromResult(profile);
    }

    private sealed class JwtDouble(bool valid) : ILoginJwtGate
    {
        public Task<bool> IsValidAndNotRevokedAsync(string token, string subject, CancellationToken cancellationToken) =>
            Task.FromResult(valid);
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<string> Messages { get; } = [];
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Messages);
        public void Dispose() { }

        private sealed class CapturingLogger(List<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) => messages.Add(formatter(state, exception));
        }
    }
}
