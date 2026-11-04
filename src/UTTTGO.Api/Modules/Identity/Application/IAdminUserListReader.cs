using System.Text;

namespace UTTTGO.Api.Modules.Identity.Application;

// Published by Identity: Admin must not access the users collection or Identity repositories directly.
public interface IAdminUserListReader
{
    Task<AdminUserPage> ListAsync(AdminUserListQuery query, CancellationToken cancellationToken);
}

public sealed record AdminUserListQuery(string? Search, string? Role, string? AccountStatus,
    int Page, int PageSize, bool CreatedAtDescending);

// CreatedAt must come from persisted profile data; never derive it from an ID or the current clock.
public sealed record AdminUserListEntry(string Id, string DisplayName, IReadOnlyList<string> Roles,
    string AccountStatus, DateTime CreatedAt);

public sealed record AdminUserPage(IReadOnlyList<AdminUserListEntry> Items, long Total);

public static class AdminUserListSelection
{
    // Shared query semantics for a future Identity storage adapter; not an in-memory production store.
    public static AdminUserPage Select(IEnumerable<AdminUserListEntry> profiles, AdminUserListQuery query)
    {
        var filtered = profiles.Where(profile =>
            (query.Search is null || profile.DisplayName.Normalize(NormalizationForm.FormKC)
                .Contains(query.Search, StringComparison.OrdinalIgnoreCase)) &&
            (query.Role is null || profile.Roles.Contains(query.Role, StringComparer.Ordinal)) &&
            (query.AccountStatus is null || profile.AccountStatus == query.AccountStatus));
        var ordered = query.CreatedAtDescending
            ? filtered.OrderByDescending(profile => profile.CreatedAt).ThenBy(profile => profile.Id, StringComparer.Ordinal)
            : filtered.OrderBy(profile => profile.CreatedAt).ThenBy(profile => profile.Id, StringComparer.Ordinal);
        var matches = ordered.ToArray();
        return new AdminUserPage(matches.Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize).ToArray(), matches.LongLength);
    }
}
