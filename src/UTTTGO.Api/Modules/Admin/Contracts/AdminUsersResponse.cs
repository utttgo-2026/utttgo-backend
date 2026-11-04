namespace UTTTGO.Api.Modules.Admin.Contracts;

public sealed record AdminUserResponse(string Id, string DisplayName, IReadOnlyList<string> Roles,
    string AccountStatus);

public sealed record AdminUsersResponse(IReadOnlyList<AdminUserResponse> Items, int Page, int PageSize,
    long Total);

public sealed record AdminUsersQueryRequest(string? Search, string? Role, string? AccountStatus,
    string? Page, string? PageSize, string? SortBy);
