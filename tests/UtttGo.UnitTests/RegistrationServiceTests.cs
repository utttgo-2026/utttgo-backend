using FluentAssertions;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Identity.Contracts;
using UTTTGO.Api.Modules.Identity.Validators;
using Xunit;

namespace UtttGo.UnitTests;

public sealed class RegistrationServiceTests
{
    [Fact]
    public async Task NewAccountStoresPendingBuyer()
    {
        var users = new RecordingUsers();
        var service = CreateService(users, new SupabaseSignUpResult(false, "subject-1", "a@uttt.edu.mx"));

        var result = await service.RegisterAsync("a@uttt.edu.mx", "not-stored", "Alice", default);

        result.Should().Be(RegistrationOutcome.Accepted);
        users.Saved.Should().Be(new PendingBuyerProfile("subject-1", "a@uttt.edu.mx", "Alice"));
        users.Saved!.Role.Should().Be("Buyer");
        users.Saved.Status.Should().Be("PendingEmailVerification");
    }

    [Fact]
    public async Task ExistingAccountDoesNotCreateProfile()
    {
        var users = new RecordingUsers();
        var service = CreateService(users, new SupabaseSignUpResult(true, null, null));

        (await service.RegisterAsync("a@uttt.edu.mx", "not-stored", "Alice", default))
            .Should().Be(RegistrationOutcome.Accepted);
        users.Saved.Should().BeNull();
    }

    [Theory]
    [InlineData(null, "a@uttt.edu.mx")]
    [InlineData("subject-1", "another@uttt.edu.mx")]
    public async Task UntrustedIdentityDoesNotCreateProfile(string? subject, string email)
    {
        var users = new RecordingUsers();
        var service = CreateService(users, new SupabaseSignUpResult(false, subject, email));

        (await service.RegisterAsync("a@uttt.edu.mx", "not-stored", "Alice", default))
            .Should().Be(RegistrationOutcome.Unavailable);
        users.Saved.Should().BeNull();
    }

    [Fact]
    public async Task StorageFailureCannotBeAccepted()
    {
        var users = new RecordingUsers { FailSave = true };
        var service = CreateService(users, new SupabaseSignUpResult(false, "subject-1", "a@uttt.edu.mx"));

        (await service.RegisterAsync("a@uttt.edu.mx", "not-stored", "Alice", default))
            .Should().Be(RegistrationOutcome.Unavailable);
    }

    [Fact]
    public async Task RedisFailureCannotBeAccepted()
    {
        var service = new RegistrationService(new FailingRateGate(),
            new FakeSupabase(new SupabaseSignUpResult(true, null, null)), new RecordingUsers());

        (await service.RegisterAsync("a@uttt.edu.mx", "not-stored", "Alice", default))
            .Should().Be(RegistrationOutcome.Unavailable);
    }

    [Fact]
    public async Task UnexpectedProviderExceptionFailsClosed()
    {
        var users = new RecordingUsers();
        var service = new RegistrationService(new AllowRateGate(), new FailingSupabase(), users);

        (await service.RegisterAsync("a@uttt.edu.mx", "private-password", "Alice", default))
            .Should().Be(RegistrationOutcome.Unavailable);
        users.Saved.Should().BeNull();
    }

    [Fact]
    public async Task UnexpectedStorageExceptionFailsClosed()
    {
        var users = new RecordingUsers { ThrowUnexpectedOnSave = true };
        var service = CreateService(users, new SupabaseSignUpResult(false, "subject-1", "a@uttt.edu.mx"));

        (await service.RegisterAsync("a@uttt.edu.mx", "private-password", "Alice", default))
            .Should().Be(RegistrationOutcome.Unavailable);
        users.Saved.Should().BeNull();
    }

    [Fact]
    public async Task MongoPreflightFailurePreventsProviderCall()
    {
        var provider = new CountingSupabase();
        var service = new RegistrationService(new AllowRateGate(), provider,
            new RecordingUsers { FailPreflight = true });

        (await service.RegisterAsync("a@uttt.edu.mx", "private-password", "Alice", default))
            .Should().Be(RegistrationOutcome.Unavailable);
        provider.Calls.Should().Be(0);
    }

    [Fact]
    public async Task ConcurrentRetryIsIdempotentOnlyWhenRepositoryEnforcesUniqueness()
    {
        var users = new UniqueUsers();
        var service = new RegistrationService(new AllowRateGate(),
            new FakeSupabase(new SupabaseSignUpResult(false, "same-subject", "a@uttt.edu.mx")), users);

        var results = await Task.WhenAll(Enumerable.Range(0, 16)
            .Select(_ => service.RegisterAsync("a@uttt.edu.mx", "private-password", "Alice", default)));

        results.Should().OnlyContain(result => result == RegistrationOutcome.Accepted);
        users.Count.Should().Be(1); // In-memory fake contract, not evidence of atomic Mongo writes.
    }

    [Theory]
    [InlineData("a@uttt.edu.mx", true)]
    [InlineData("a@UTTT.EDU.MX", true)]
    [InlineData("a@sub.uttt.edu.mx", false)]
    [InlineData("a@other@uttt.edu.mx", false)]
    [InlineData("a@uttt.edu.mx.attacker", false)]
    public void DomainMustMatchExactly(string email, bool expected) =>
        RegisterRequestValidator.HasInstitutionalDomain(email).Should().Be(expected);

    [Fact]
    public void ExtraFieldsAreRejected()
    {
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest
        {
            Email = "a@uttt.edu.mx",
            Password = "not-stored",
            DisplayName = "Alice",
            UnknownFields = new Dictionary<string, System.Text.Json.JsonElement> { ["roles"] = default }
        };
        validator.Validate(request).IsValid.Should().BeFalse();
    }

    private static RegistrationService CreateService(RecordingUsers users, SupabaseSignUpResult signUp) =>
        new(new AllowRateGate(), new FakeSupabase(signUp), users);

    private sealed class AllowRateGate : IAuthRateLimitGate
    {
        public Task<bool> IsAllowedAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class FailingRateGate : IAuthRateLimitGate
    {
        public Task<bool> IsAllowedAsync(CancellationToken cancellationToken) =>
            throw new ExternalDependencyUnavailableException();
    }

    private sealed class FakeSupabase(SupabaseSignUpResult result) : ISupabaseRegistration
    {
        public Task<SupabaseSignUpResult> SignUpAsync(string email, string password,
            CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class FailingSupabase : ISupabaseRegistration
    {
        public Task<SupabaseSignUpResult> SignUpAsync(string email, string password,
            CancellationToken cancellationToken) => throw new InvalidOperationException("private-password internal detail");
    }

    private sealed class CountingSupabase : ISupabaseRegistration
    {
        public int Calls { get; private set; }
        public Task<SupabaseSignUpResult> SignUpAsync(string email, string password,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new SupabaseSignUpResult(false, "subject-1", email));
        }
    }

    private sealed class UniqueUsers : IUserRegistrationRepository
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, PendingBuyerProfile> profiles = new();
        public int Count => profiles.Count;
        public Task EnsureAvailableAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SavePendingBuyerAsync(PendingBuyerProfile profile, CancellationToken cancellationToken)
        {
            profiles.TryAdd(profile.SupabaseUserId, profile);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingUsers : IUserRegistrationRepository
    {
        public bool FailSave { get; init; }
        public bool FailPreflight { get; init; }
        public bool ThrowUnexpectedOnSave { get; init; }
        public PendingBuyerProfile? Saved { get; private set; }
        public Task EnsureAvailableAsync(CancellationToken cancellationToken) => FailPreflight
            ? throw new ExternalDependencyUnavailableException()
            : Task.CompletedTask;
        public Task SavePendingBuyerAsync(PendingBuyerProfile profile, CancellationToken cancellationToken)
        {
            if (ThrowUnexpectedOnSave) throw new InvalidOperationException("private-password internal storage failure");
            if (FailSave) throw new ExternalDependencyUnavailableException();
            Saved = profile;
            return Task.CompletedTask;
        }
    }
}
