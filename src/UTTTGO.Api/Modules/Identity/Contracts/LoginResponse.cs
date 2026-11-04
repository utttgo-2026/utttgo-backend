namespace UTTTGO.Api.Modules.Identity.Contracts;

public sealed record LoginResponse(string AccessToken, long ExpiresIn, string TokenType, LoginUserResponse User);

public sealed record LoginUserResponse(string Id, IReadOnlyList<string> Roles, string AccountStatus);
