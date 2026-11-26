using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using UTTTGO.Api.Modules.Catalog.Application;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Media.Application;
using UTTTGO.Api.Modules.Notifications.Application;
using UTTTGO.Api.Modules.Orders.Application;
using Xunit;

namespace UtttGo.UnitTests;

public sealed class DeleteOwnAccountServiceTests
{
    private static readonly LocalLoginProfile Active = new("user-1", "subject-1", "user@uttt.edu.mx",
        "Active", ["Buyer", "Seller"], "User", "5512345678", "avatar-1", 4, "seller-1");

    [Fact]
    public async Task DeletesOwnProfilePausesProductsAndRevokesCurrentToken()
    {
        var dependencies = new Dependencies(Active);
        var result = await dependencies.Service.DeleteAsync(Token(), default);

        result.Outcome.Should().Be(DeleteOwnAccountOutcome.Success);
        dependencies.Accounts.Calls.Should().Be(1);
        dependencies.Catalog.SellerId.Should().Be("seller-1");
        dependencies.Media.OwnerUserId.Should().Be("user-1");
        dependencies.Media.DeletionReference.Should().NotBeNullOrWhiteSpace();
        dependencies.Revocations.Calls.Should().Be(1);
        dependencies.Supabase.Calls.Should().Be(1);
        dependencies.Accounts.DeletedRef.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ActiveOrderStopsBeforeAnyMutation()
    {
        var dependencies = new Dependencies(Active) { Orders = new ActiveOrders(true) };
        var result = await dependencies.Service.DeleteAsync(Token(), default);

        result.Outcome.Should().Be(DeleteOwnAccountOutcome.Conflict);
        dependencies.Accounts.Calls.Should().Be(0);
        dependencies.Catalog.SellerId.Should().BeNull();
        dependencies.Revocations.Calls.Should().Be(0);
    }

    [Fact]
    public async Task RetryForDeletedProfileRepeatsOwnerAndRevocationSteps()
    {
        var deleted = Active with { AccountStatus = "Deleted", DeletedUserRef = "stable-ref", Version = 5 };
        var dependencies = new Dependencies(deleted);
        var result = await dependencies.Service.DeleteAsync(Token(), default);

        result.Outcome.Should().Be(DeleteOwnAccountOutcome.Success);
        dependencies.Accounts.Calls.Should().Be(0);
        dependencies.Catalog.SellerId.Should().Be("seller-1");
        dependencies.Media.OwnerUserId.Should().Be("user-1");
        dependencies.Revocations.Calls.Should().Be(1);
    }

    [Fact]
    public async Task RedisFailureIsFailClosedAfterLocalChanges()
    {
        var dependencies = new Dependencies(Active) { Revocations = new Revocations(false) };
        var result = await dependencies.Service.DeleteAsync(Token(), default);

        result.Outcome.Should().Be(DeleteOwnAccountOutcome.Unavailable);
        dependencies.Supabase.Calls.Should().Be(0);
    }

    [Fact]
    public async Task DeviceTokenCleanupFailureDoesNotBlockSuccessfulDeletion()
    {
        var dependencies = new Dependencies(Active)
        {
            DeviceTokens = new DeviceTokens { ThrowOnRemove = true }
        };

        var result = await dependencies.Service.DeleteAsync(Token(), default);

        result.Outcome.Should().Be(DeleteOwnAccountOutcome.Success);
        dependencies.DeviceTokens.Calls.Should().Be(1);
        dependencies.Supabase.Calls.Should().Be(1);
    }

    [Fact]
    public async Task SupabaseLogoutFailureDoesNotBlockSuccessfulDeletion()
    {
        var dependencies = new Dependencies(Active)
        {
            Supabase = new Supabase { ThrowOnRevoke = true }
        };

        var result = await dependencies.Service.DeleteAsync(Token(), default);

        result.Outcome.Should().Be(DeleteOwnAccountOutcome.Success);
        dependencies.Revocations.Calls.Should().Be(1);
        dependencies.Supabase.Calls.Should().Be(1);
    }

    [Fact]
    public async Task MissingOrInvalidSessionIsUnauthorized()
    {
        var dependencies = new Dependencies(Active) { Profiles = new Profiles(AuthenticatedLocalProfileOutcome.Unauthorized) };
        var result = await dependencies.Service.DeleteAsync(Token(), default);

        result.Outcome.Should().Be(DeleteOwnAccountOutcome.Unauthorized);
        dependencies.Accounts.Calls.Should().Be(0);
    }

    private static string Token()
    {
        var token = new JwtSecurityToken(issuer: "tests", audience: "tests", claims:
            [new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, "subject-1"),
             new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti, "jti-1")],
            expires: DateTime.UtcNow.AddMinutes(5));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class Dependencies(LocalLoginProfile profile)
    {
        public Profiles Profiles { get; set; } = new(profile);
        public Accounts Accounts { get; } = new(profile);
        public ActiveOrders Orders { get; set; } = new(false);
        public Catalog Catalog { get; } = new();
        public Revocations Revocations { get; set; } = new(true);
        public Supabase Supabase { get; set; } = new();
        public Media Media { get; } = new();
        public DeviceTokens DeviceTokens { get; set; } = new();
        public DeleteOwnAccountService Service => new(Profiles, Accounts, Orders, Catalog, Revocations, Supabase,
            Media, DeviceTokens);
    }

    private sealed class Profiles(AuthenticatedLocalProfileOutcome outcome, LocalLoginProfile? profile = null)
        : IAuthenticatedLocalProfileReader
    {
        public Profiles(LocalLoginProfile profile) : this(AuthenticatedLocalProfileOutcome.Success, profile) { }
        public Task<AuthenticatedLocalProfileResult> ReadAsync(string token, CancellationToken cancellationToken) =>
            Task.FromResult(new AuthenticatedLocalProfileResult(outcome, profile));
    }

    private sealed class Accounts(LocalLoginProfile profile) : IAccountDeletionRepository
    {
        public int Calls { get; private set; }
        public string? DeletedRef { get; private set; }
        public Task<AccountDeletionResult> DeleteOwnAsync(string subject, string localId, long expectedVersion,
            string deletedUserRef, CancellationToken cancellationToken)
        {
            Calls++;
            DeletedRef = deletedUserRef;
            return Task.FromResult(new AccountDeletionResult(AccountDeletionOutcome.Deleted,
                profile with { AccountStatus = "Deleted", DeletedUserRef = deletedUserRef, Version = expectedVersion + 1 }));
        }
    }

    private sealed class ActiveOrders(bool active) : IActiveOrderReader
    {
        public Task<bool> HasActiveOrdersAsync(string buyerUserId, string? sellerId, CancellationToken cancellationToken) =>
            Task.FromResult(active);
    }

    private sealed class Catalog : IAccountDeletionCatalog
    {
        public string? SellerId { get; private set; }
        public Task PauseProductsForSellerAsync(string sellerId, CancellationToken cancellationToken)
        {
            SellerId = sellerId;
            return Task.CompletedTask;
        }
    }

    private sealed class Revocations(bool succeeds) : IJwtRevocationStore
    {
        public int Calls { get; private set; }
        public Task<bool> TryRevokeAsync(string jti, TimeSpan ttl, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(succeeds);
        }
    }

    private sealed class Supabase : ISupabaseSessionRevoker
    {
        public int Calls { get; private set; }
        public bool ThrowOnRevoke { get; init; }
        public Task RevokeGloballyAsync(string accessToken, CancellationToken cancellationToken)
        {
            Calls++;
            if (ThrowOnRevoke) throw new InvalidOperationException("test logout failure");
            return Task.CompletedTask;
        }
    }

    private sealed class DeviceTokens : IDeviceTokenStore
    {
        public int Calls { get; private set; }
        public bool ThrowOnRemove { get; init; }
        public Task<DeviceTokenRegistration> UpsertAsync(string userId, string token, string installationId,
            string platform, string? appVersion, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> RemoveAsync(string userId, string deviceTokenId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task RemoveForUserAsync(string userId, CancellationToken cancellationToken)
        {
            Calls++;
            if (ThrowOnRemove) throw new InvalidOperationException("test token cleanup failure");
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<DeviceTokenRecord>> FindActiveAsync(string userId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeactivateAsync(string deviceTokenId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public string Unprotect(string protectedToken) => throw new NotSupportedException();
    }

    private sealed class Media : IAccountDeletionMedia
    {
        public string? OwnerUserId { get; private set; }
        public string? DeletionReference { get; private set; }

        public Task EnqueueOwnerDeletionAsync(string ownerUserId, string deletionReference,
            CancellationToken cancellationToken)
        {
            OwnerUserId = ownerUserId;
            DeletionReference = deletionReference;
            return Task.CompletedTask;
        }
    }
}
