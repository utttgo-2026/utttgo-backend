namespace UTTTGO.Api.Modules.Identity.Application;

public interface IAdminUserStatusWriter
{
    Task<AdminUserStatusTarget?> FindAsync(string userId, CancellationToken cancellationToken);
    Task<AdminUserStatusWriteResult> SetStatusAsync(string userId, string expectedStatus,
        string newStatus, CancellationToken cancellationToken);
}

public sealed record AdminUserStatusTarget(string Id, string SupabaseUserId, string AccountStatus,
    string? SellerId);

public enum AdminUserStatusWriteOutcome { Updated, Unchanged, Missing, Conflict }

public sealed record AdminUserStatusWriteResult(AdminUserStatusWriteOutcome Outcome,
    AdminUserStatusTarget? Target = null, string? PreviousStatus = null);

// This is deliberately narrower than the session implementation: Admin never owns session data.
public interface IAccountSessionRevoker
{
    Task RevokeAllAsync(string supabaseUserId, CancellationToken cancellationToken);
}

public interface IActiveSessionRegistry
{
    Task RegisterAsync(string supabaseUserId, string revocationId, long expiresAtUnix,
        CancellationToken cancellationToken);
}
