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
        var result = await dependencies.Service.DeleteAsync(Token(), OperationId(), Proof(), default);

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
        var result = await dependencies.Service.DeleteAsync(Token(), OperationId(), Proof(), default);

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
        var result = await dependencies.Service.DeleteAsync(Token(), OperationId(), Proof(), default);

        result.Outcome.Should().Be(DeleteOwnAccountOutcome.Success);
        dependencies.Accounts.Calls.Should().Be(1);
        dependencies.Catalog.SellerId.Should().Be("seller-1");
        dependencies.Media.OwnerUserId.Should().Be("user-1");
        dependencies.Revocations.Calls.Should().Be(1);
    }

    [Fact]
    public async Task RedisFailureIsFailClosedAfterLocalChanges()
    {
        var dependencies = new Dependencies(Active) { Revocations = new Revocations(false) };
        var result = await dependencies.Service.DeleteAsync(Token(), OperationId(), Proof(), default);

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

        var result = await dependencies.Service.DeleteAsync(Token(), OperationId(), Proof(), default);

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

        var result = await dependencies.Service.DeleteAsync(Token(), OperationId(), Proof(), default);

        result.Outcome.Should().Be(DeleteOwnAccountOutcome.Success);
        dependencies.Revocations.Calls.Should().Be(1);
        dependencies.Supabase.Calls.Should().Be(1);
    }

    [Fact]
    public async Task RecentAuthenticationIsRequiredBeforeCreatingAnOperation()
    {
        var dependencies = new Dependencies(Active);
        var result = await dependencies.Service.DeleteAsync(Token(false), OperationId(), Proof(), default);

        result.Outcome.Should().Be(DeleteOwnAccountOutcome.Unauthorized);
        dependencies.Accounts.Calls.Should().Be(0);
        dependencies.Operations.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task CatalogFailureLeavesARecoverableOperation()
    {
        var dependencies = new Dependencies(Active) { Catalog = new Catalog { ThrowOnPause = true } };
        var result = await dependencies.Service.DeleteAsync(Token(), OperationId(), Proof(), default);

        result.Outcome.Should().Be(DeleteOwnAccountOutcome.Unavailable);
        result.Operation!.Status.Should().Be(AccountDeletionOperationStates.RequiresIntervention);
        result.Operation.Stage.Should().Be(AccountDeletionStages.ProfileDeleted);
    }

    [Fact]
    public async Task MongoFailureAfterRevocationLeavesTheAccountUnchangedAndRecoverable()
    {
        var dependencies = new Dependencies(Active) { Accounts = new Accounts(Active) { ThrowOnDelete = true } };
        var result = await dependencies.Service.DeleteAsync(Token(), OperationId(), Proof(), default);

        result.Outcome.Should().Be(DeleteOwnAccountOutcome.Unavailable);
        result.Operation!.Stage.Should().Be(AccountDeletionStages.SessionRevoked);
        dependencies.Catalog.SellerId.Should().BeNull();
    }

    [Fact]
    public async Task MediaFailureLeavesProductsPausedAndCanBeRecovered()
    {
        var dependencies = new Dependencies(Active) { Media = new Media { ThrowOnEnqueue = true } };
        var result = await dependencies.Service.DeleteAsync(Token(), OperationId(), Proof(), default);

        result.Outcome.Should().Be(DeleteOwnAccountOutcome.Unavailable);
        result.Operation!.Stage.Should().Be(AccountDeletionStages.ProductsPaused);
    }

    [Fact]
    public async Task MissingOrInvalidSessionIsUnauthorized()
    {
        var dependencies = new Dependencies(Active) { Profiles = new Profiles(AuthenticatedLocalProfileOutcome.Unauthorized) };
        var result = await dependencies.Service.DeleteAsync(Token(), OperationId(), Proof(), default);

        result.Outcome.Should().Be(DeleteOwnAccountOutcome.Unauthorized);
        dependencies.Accounts.Calls.Should().Be(0);
    }

    private static string Token(bool recent = true)
    {
        var token = new JwtSecurityToken(issuer: "tests", audience: "tests", claims:
             [new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, "subject-1"),
              new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti, "jti-1"),
              new("auth_time", (recent ? DateTimeOffset.UtcNow : DateTimeOffset.UtcNow.AddMinutes(-20))
                  .ToUnixTimeSeconds().ToString())],
            expires: DateTime.UtcNow.AddMinutes(5));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string OperationId() => "11111111-1111-1111-1111-111111111111";
    private static string Proof() => "01234567890123456789012345678901";

    private sealed class Dependencies(LocalLoginProfile profile)
    {
        public Profiles Profiles { get; set; } = new(profile);
        public Accounts Accounts { get; set; } = new(profile);
        public ActiveOrders Orders { get; set; } = new(false);
        public Catalog Catalog { get; set; } = new();
        public Revocations Revocations { get; set; } = new(true);
        public Supabase Supabase { get; set; } = new();
        public Media Media { get; set; } = new();
        public DeviceTokens DeviceTokens { get; set; } = new();
        public Operations Operations { get; } = new();
        public DeleteOwnAccountService Service => new(Profiles, Accounts, Orders, Catalog, Revocations, Supabase,
            Media, Operations, DeviceTokens);
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
        public bool ThrowOnDelete { get; init; }
        public Task<AccountDeletionResult> DeleteOwnAsync(string subject, string localId, long expectedVersion,
            string deletedUserRef, CancellationToken cancellationToken)
        {
            Calls++;
            if (ThrowOnDelete) throw new ExternalDependencyUnavailableException();
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
        public bool ThrowOnPause { get; init; }
        public Task PauseProductsForSellerAsync(string sellerId, CancellationToken cancellationToken)
        {
            if (ThrowOnPause) throw new ExternalDependencyUnavailableException();
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
        public bool ThrowOnEnqueue { get; init; }

        public Task EnqueueOwnerDeletionAsync(string ownerUserId, string deletionReference,
            CancellationToken cancellationToken)
        {
            if (ThrowOnEnqueue) throw new ExternalDependencyUnavailableException();
            OwnerUserId = ownerUserId;
            DeletionReference = deletionReference;
            return Task.CompletedTask;
        }
    }

    private sealed class Operations : IAccountDeletionOperationStore
    {
        private readonly Dictionary<string, AccountDeletionOperation> items = new();
        public IReadOnlyDictionary<string, AccountDeletionOperation> Items => items;

        public Task<AccountDeletionOperation?> CreateOrGetAsync(string operationId, string proofHash, string subject,
            string userId, string? sellerId, long expectedVersion, string deletedUserRef, string revocationId,
            long expiration, CancellationToken cancellationToken)
        {
            if (items.TryGetValue(operationId, out var existing)) return Task.FromResult<AccountDeletionOperation?>(existing);
            var now = DateTime.UtcNow;
            var item = new AccountDeletionOperation(operationId, userId, subject, sellerId, expectedVersion,
                deletedUserRef, revocationId, expiration, AccountDeletionOperationStates.Pending,
                AccountDeletionStages.Created, null, now, now);
            items[operationId] = item;
            return Task.FromResult<AccountDeletionOperation?>(item);
        }

        public Task<AccountDeletionOperation?> FindAsync(string operationId, string proofHash,
            CancellationToken cancellationToken) => Task.FromResult(items.GetValueOrDefault(operationId));

        public Task<IReadOnlyList<AccountDeletionOperation>> ListRecoverableAsync(
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AccountDeletionOperation>>(
            items.Values.Where(item => item.Status != AccountDeletionOperationStates.Completed).ToArray());

        public Task<AccountDeletionOperation?> MarkAsync(string operationId, string status, string stage,
            string? failureCode, CancellationToken cancellationToken)
        {
            if (!items.TryGetValue(operationId, out var item)) return Task.FromResult<AccountDeletionOperation?>(null);
            var updated = item with
            {
                Status = status,
                Stage = stage,
                FailureCode = failureCode,
                UpdatedAt = DateTime.UtcNow
            };
            items[operationId] = updated;
            return Task.FromResult<AccountDeletionOperation?>(updated);
        }

        public Task<AccountDeletionOperation?> MarkFailureAsync(string operationId, string status,
            string? failureCode, CancellationToken cancellationToken)
        {
            if (!items.TryGetValue(operationId, out var item)) return Task.FromResult<AccountDeletionOperation?>(null);
            var updated = item with { Status = status, FailureCode = failureCode, UpdatedAt = DateTime.UtcNow };
            items[operationId] = updated;
            return Task.FromResult<AccountDeletionOperation?>(updated);
        }
    }
}
