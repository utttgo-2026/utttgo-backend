using UTTTGO.Api.Modules.Admin.Contracts;
using UTTTGO.Api.Modules.Identity.Application;

namespace UTTTGO.Api.Modules.Admin.Application;

public enum AdminUsersOutcome { Success, Unauthorized, Forbidden, RateLimited, Unavailable }

public sealed record AdminUsersResult(AdminUsersOutcome Outcome, AdminUsersResponse? Response = null);

public interface IAdminUsersService
{
    Task<AdminUsersResult> ListAsync(string token, AdminUserListQuery query, string correlationId,
        CancellationToken cancellationToken);
}

public interface IAdminUsersRateLimitGate
{
    Task<bool> IsAllowedAsync(string actorId, CancellationToken cancellationToken);
}

public interface IAdminUsersAudit
{
    Task RecordAsync(string actorId, string correlationId, string result, CancellationToken cancellationToken);
}

public sealed class AdminUsersService(ISessionService session, IAdminUsersRateLimitGate rateLimit,
    IAdminUserListReader reader, IAdminUsersAudit audit) : IAdminUsersService
{
    public async Task<AdminUsersResult> ListAsync(string token, AdminUserListQuery query,
        string correlationId, CancellationToken cancellationToken)
    {
        try
        {
            var authorization = await session.AuthenticateAsync(token, cancellationToken);
            if (authorization.Outcome == SessionOutcome.Unavailable) return new(AdminUsersOutcome.Unavailable);
            if (authorization.Outcome != SessionOutcome.Success || authorization.Profile is null ||
                authorization.Profile.AccountStatus != "Active") return new(AdminUsersOutcome.Unauthorized);
            var profile = authorization.Profile;
            if (!profile.Roles.Contains("Admin", StringComparer.Ordinal))
                return new(AdminUsersOutcome.Forbidden);
            if (!await rateLimit.IsAllowedAsync(profile.Id, cancellationToken))
                return new(AdminUsersOutcome.RateLimited);

            var page = await reader.ListAsync(query, cancellationToken);
            if (page.Items is null || page.Total < 0 || page.Items.Count > query.PageSize ||
                page.Items.Count > page.Total || page.Items.Any(item =>
                    string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.DisplayName) ||
                    item.Roles is null || item.Roles.Count == 0 ||
                    item.AccountStatus is not ("Active" or "PendingEmailVerification" or "Suspended") ||
                    item.CreatedAt.Kind != DateTimeKind.Utc)) return new(AdminUsersOutcome.Unavailable);

            var response = new AdminUsersResponse(page.Items.Select(item =>
                new AdminUserResponse(item.Id, item.DisplayName, item.Roles.ToArray(), item.AccountStatus))
                .ToArray(), query.Page, query.PageSize, page.Total);
            // No successful response until the mandatory audit has completed.
            await audit.RecordAsync(profile.Id, correlationId, "Listed", cancellationToken);
            return new(AdminUsersOutcome.Success, response);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new(AdminUsersOutcome.Unavailable);
        }
    }
}
