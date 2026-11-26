using System.Security.Cryptography;
using System.Text;

namespace UTTTGO.Api.Modules.Identity.Application;

public static class AccountDeletionOperationStates
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Completed = "Completed";
    public const string RequiresIntervention = "RequiresIntervention";
}

public static class AccountDeletionStages
{
    public const string Created = "Created";
    public const string SessionRevoked = "SessionRevoked";
    public const string ProfileDeleted = "ProfileDeleted";
    public const string ProductsPaused = "ProductsPaused";
    public const string MediaQueued = "MediaQueued";
    public const string Completed = "Completed";
}

public sealed record AccountDeletionOperation(
    string OperationId,
    string UserId,
    string Subject,
    string? SellerId,
    long ExpectedVersion,
    string DeletedUserRef,
    string RevocationId,
    long Expiration,
    string Status,
    string Stage,
    string? FailureCode,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public interface IAccountDeletionOperationStore
{
    Task<AccountDeletionOperation?> CreateOrGetAsync(
        string operationId, string proofHash, string subject, string userId, string? sellerId,
        long expectedVersion, string deletedUserRef, string revocationId, long expiration,
        CancellationToken cancellationToken);

    Task<AccountDeletionOperation?> FindAsync(string operationId, string proofHash,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AccountDeletionOperation>> ListRecoverableAsync(
        CancellationToken cancellationToken);

    Task<AccountDeletionOperation?> MarkAsync(string operationId, string status, string stage,
        string? failureCode, CancellationToken cancellationToken);

    Task<AccountDeletionOperation?> MarkFailureAsync(string operationId, string status,
        string? failureCode, CancellationToken cancellationToken);
}

public interface IAccountDeletionRecovery
{
    Task RecoverAsync(AccountDeletionOperation operation, CancellationToken cancellationToken);
}

public static class AccountDeletionProof
{
    public static string Hash(string proof) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(proof)));
}
