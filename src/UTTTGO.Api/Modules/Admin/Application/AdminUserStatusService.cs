using UTTTGO.Api.Modules.Admin.Contracts;
using UTTTGO.Api.Modules.Catalog.Application;
using UTTTGO.Api.Modules.Identity.Application;

namespace UTTTGO.Api.Modules.Admin.Application;

public enum AdminUserStatusOutcome
{
    Success, Unauthorized, Forbidden, SelfTarget, NotFound,
    Unchanged, RateLimited, Unavailable
}
public sealed record AdminUserStatusResult(AdminUserStatusOutcome Outcome,
    AdminUserStatusResponse? Response = null);

public interface IAdminUserStatusService
{
    Task<AdminUserStatusResult> SetAsync(string token, string userId, string status,
        string correlationId, CancellationToken cancellationToken);
}

public interface IAdminUserStatusRateLimitGate
{
    Task<bool> IsAllowedAsync(string actorId, CancellationToken cancellationToken);
}

public sealed record AdminUserStatusAudit(string ActorId, string TargetId, string Action,
    string PreviousStatus, string NewStatus, DateTime OccurredAtUtc, string CorrelationId);
public interface IAdminUserStatusAudit
{
    Task RecordAsync(AdminUserStatusAudit audit, CancellationToken cancellationToken);
}

public sealed class AdminUserStatusService(ISessionService session,
    IAdminUserStatusRateLimitGate rateLimit, IAdminUserStatusWriter writer,
    IAccountSessionRevoker sessions, IDeviceTokenStatusRemover deviceTokens,
    IAccountStatusCatalog catalog, IAdminUserStatusAudit audit) : IAdminUserStatusService
{
    public async Task<AdminUserStatusResult> SetAsync(string token, string userId, string status,
        string correlationId, CancellationToken cancellationToken)
    {
        try
        {
            var authorization = await session.AuthenticateAsync(token, cancellationToken);
            if (authorization.Outcome == SessionOutcome.Unavailable) return new(AdminUserStatusOutcome.Unavailable);
            if (authorization.Outcome != SessionOutcome.Success || authorization.Profile is null)
                return new(AdminUserStatusOutcome.Unauthorized);
            var actor = authorization.Profile;
            if (!actor.Roles.Contains("Admin", StringComparer.Ordinal)) return new(AdminUserStatusOutcome.Forbidden);
            if (actor.AccountStatus != "Active") return new(AdminUserStatusOutcome.Unauthorized);
            if (actor.Id == userId) return new(AdminUserStatusOutcome.SelfTarget);
            if (!await rateLimit.IsAllowedAsync(actor.Id, cancellationToken)) return new(AdminUserStatusOutcome.RateLimited);

            var target = await writer.FindAsync(userId, cancellationToken);
            if (target is null) return new(AdminUserStatusOutcome.NotFound);
            if (target.AccountStatus == status)
            {
                return new(AdminUserStatusOutcome.Success, new(userId, status));
            }

            var write = await writer.SetStatusAsync(userId, target.AccountStatus, status, cancellationToken);
            if (write.Outcome == AdminUserStatusWriteOutcome.Missing) return new(AdminUserStatusOutcome.NotFound);
            if (write.Outcome == AdminUserStatusWriteOutcome.Conflict) return new(AdminUserStatusOutcome.Unavailable);
            if (write.Outcome == AdminUserStatusWriteOutcome.Unchanged)
                return new(AdminUserStatusOutcome.Success, new(userId, status));

            if (status == "Suspended")
            {
                await sessions.RevokeAllAsync(target.SupabaseUserId, cancellationToken);
                await deviceTokens.RemoveForUserAsync(userId, cancellationToken);
                if (!string.IsNullOrWhiteSpace(target.SellerId))
                    await catalog.PauseProductsForSellerAsync(target.SellerId, cancellationToken);
            }
            await audit.RecordAsync(new(actor.Id, userId, status == "Suspended" ? "Suspended" : "Reactivated",
                target.AccountStatus, status, DateTime.UtcNow, correlationId), cancellationToken);
            return new(AdminUserStatusOutcome.Success, new(userId, status));
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        { return new(AdminUserStatusOutcome.Unavailable); }
    }
}

public interface IDeviceTokenStatusRemover
{
    Task RemoveForUserAsync(string userId, CancellationToken cancellationToken);
}
