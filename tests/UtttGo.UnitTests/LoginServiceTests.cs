using FluentAssertions;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Identity.Contracts;
using UTTTGO.Api.Modules.Identity.Validators;
using Xunit;

namespace UtttGo.UnitTests;

public sealed class LoginServiceTests
{
    private const string Email = "a@uttt.edu.mx";
    private const string Token = "test-only-jwt";
    private static readonly SupabaseLoginResult Authenticated = new(true, true, "sub-1", Email, Token, 3600);
    private static readonly LocalLoginProfile Active = new("local-1", "sub-1", Email, "Active", ["Seller"]);

    [Fact]
    public async Task VerifiedActiveProfileReturnsOnlyLocalRolesAndSupabaseToken()
    {
        var result = await CreateService(Authenticated, Active).LoginAsync(Email, "private-password", default);

        result.Outcome.Should().Be(LoginOutcome.Success);
        result.Response.Should().BeEquivalentTo(new LoginResponse(Token, 3600, "Bearer",
            new LoginUserResponse("local-1", ["Seller"], "Active")));
        result.Response!.User.Roles.Should().Equal("Seller");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task InvalidOrUnconfirmedProviderResponseNeverReturnsToken(bool valid, bool confirmed)
    {
        var result = await CreateService(Authenticated with { CredentialsValid = valid, EmailConfirmed = confirmed },
            Active).LoginAsync(Email, "private-password", default);

        result.Outcome.Should().Be(LoginOutcome.InvalidCredentials);
        result.Response.Should().BeNull();
    }

    [Theory]
    [InlineData("PendingEmailVerification")]
    [InlineData("Suspended")]
    [InlineData("Deleted")]
    public async Task InactiveProfileNeverReturnsToken(string status)
    {
        var result = await CreateService(Authenticated, Active with { AccountStatus = status })
            .LoginAsync(Email, "private-password", default);

        result.Outcome.Should().Be(LoginOutcome.InvalidCredentials);
        result.Response.Should().BeNull();
    }

    [Fact]
    public async Task MissingProfileDoesNotAutoCreateOrReturnToken()
    {
        var result = await CreateService(Authenticated, null).LoginAsync(Email, "private-password", default);

        result.Outcome.Should().Be(LoginOutcome.InvalidCredentials);
        result.Response.Should().BeNull();
    }

    [Fact]
    public async Task ExternalDomainGetsGenericErrorWithoutSession()
    {
        var result = await CreateService(Authenticated, Active)
            .LoginAsync("a@other.edu.mx", "private-password", default);

        result.Outcome.Should().Be(LoginOutcome.InvalidCredentials);
        result.Response.Should().BeNull();
    }

    [Theory]
    [InlineData("different-sub", "a@uttt.edu.mx")]
    [InlineData("sub-1", "different@uttt.edu.mx")]
    public async Task MismatchedProfileIsRejected(string subject, string email)
    {
        var result = await CreateService(Authenticated, Active with { SupabaseUserId = subject, Email = email })
            .LoginAsync(Email, "private-password", default);

        result.Outcome.Should().Be(LoginOutcome.InvalidCredentials);
    }

    [Theory]
    [InlineData(null, "a@uttt.edu.mx")]
    [InlineData("sub-1", "another@uttt.edu.mx")]
    public async Task ProviderIdentityMustMatchRequestedEmail(string? subject, string email)
    {
        var result = await CreateService(Authenticated with { Subject = subject, Email = email }, Active)
            .LoginAsync(Email, "private-password", default);

        result.Outcome.Should().Be(LoginOutcome.InvalidCredentials);
        result.Response.Should().BeNull();
    }

    [Theory]
    [InlineData(null, 3600)]
    [InlineData("test-only-jwt", 0)]
    public async Task MissingOrExpiredTokenCannotBeReturned(string? token, long expiresIn)
    {
        var result = await CreateService(Authenticated with { AccessToken = token, ExpiresIn = expiresIn }, Active)
            .LoginAsync(Email, "private-password", default);

        result.Outcome.Should().Be(LoginOutcome.Unavailable);
        result.Response.Should().BeNull();
    }

    [Fact]
    public async Task RevokedOrUnverifiedJwtIsRejected()
    {
        var service = new LoginService(new TestGate(), new TestSupabase(Authenticated),
            new TestProfiles(Active), new TestJwtGate(false));

        var result = await service.LoginAsync(Email, "private-password", default);
        result.Outcome.Should().Be(LoginOutcome.InvalidCredentials);
        result.Response.Should().BeNull();
    }

    [Fact]
    public async Task RedisFailureStopsAuthentication()
    {
        var service = new LoginService(new TestGate(throws: true), new TestSupabase(Authenticated),
            new TestProfiles(Active), new TestJwtGate());

        (await service.LoginAsync(Email, "private-password", default)).Outcome.Should().Be(LoginOutcome.Unavailable);
    }

    [Fact]
    public async Task RepositoryFailureDoesNotReturnSession()
    {
        var service = new LoginService(new TestGate(), new TestSupabase(Authenticated),
            new TestProfiles(Active, throws: true), new TestJwtGate());

        (await service.LoginAsync(Email, "private-password", default)).Response.Should().BeNull();
    }

    [Fact]
    public async Task TokenGateFailureDoesNotReturnSession()
    {
        var service = new LoginService(new TestGate(), new TestSupabase(Authenticated),
            new TestProfiles(Active), new TestJwtGate(throws: true));

        (await service.LoginAsync(Email, "private-password", default)).Outcome.Should().Be(LoginOutcome.Unavailable);
    }

    [Fact]
    public async Task RateLimitDenialDoesNotReturnSession()
    {
        var service = new LoginService(new TestGate(allowed: false), new TestSupabase(Authenticated),
            new TestProfiles(Active), new TestJwtGate());

        (await service.LoginAsync(Email, "private-password", default)).Outcome.Should().Be(LoginOutcome.RateLimited);
    }

    [Fact]
    public void PrivilegedFieldsFailValidation()
    {
        var request = new LoginRequest
        {
            Email = Email,
            Password = "private-password",
            UnknownFields = new Dictionary<string, System.Text.Json.JsonElement> { ["roles"] = default }
        };

        new LoginRequestValidator().Validate(request).IsValid.Should().BeFalse();
    }

    private static LoginService CreateService(SupabaseLoginResult provider, LocalLoginProfile? profile) =>
        new(new TestGate(), new TestSupabase(provider), new TestProfiles(profile), new TestJwtGate());

    private sealed class TestGate(bool allowed = true, bool throws = false) : IAuthRateLimitGate
    {
        public Task<bool> IsAllowedAsync(CancellationToken cancellationToken) => throws
            ? throw new ExternalDependencyUnavailableException()
            : Task.FromResult(allowed);
    }

    private sealed class TestSupabase(SupabaseLoginResult result) : ISupabaseLogin
    {
        public Task<SupabaseLoginResult> AuthenticateAndCheckConfirmationAsync(string email, string password,
            CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class TestProfiles(LocalLoginProfile? profile, bool throws = false) : ILoginProfileRepository
    {
        public Task<LocalLoginProfile?> FindBySubjectAsync(string subject, CancellationToken cancellationToken) =>
            throws ? throw new ExternalDependencyUnavailableException() : Task.FromResult(profile);
    }

    private sealed class TestJwtGate(bool valid = true, bool throws = false) : ILoginJwtGate
    {
        public Task<bool> IsValidAndNotRevokedAsync(string token, string subject, CancellationToken cancellationToken) =>
            throws ? throw new ExternalDependencyUnavailableException() : Task.FromResult(valid);
    }
}
