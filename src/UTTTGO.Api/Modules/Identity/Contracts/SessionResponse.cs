namespace UTTTGO.Api.Modules.Identity.Contracts;

// Session identity only; the editable user profile belongs to API-006.
public sealed record SessionResponse(string Id, string Email, string DisplayName,
    IReadOnlyList<string> Roles, string AccountStatus);
