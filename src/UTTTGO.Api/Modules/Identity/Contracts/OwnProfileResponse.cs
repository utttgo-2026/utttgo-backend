namespace UTTTGO.Api.Modules.Identity.Contracts;

// Private self-profile read contract. Does not include account status or session details.
public sealed record OwnProfileResponse(string Id, string DisplayName, string Email,
    string? Phone, string? AvatarUrl, IReadOnlyList<string> Roles);
