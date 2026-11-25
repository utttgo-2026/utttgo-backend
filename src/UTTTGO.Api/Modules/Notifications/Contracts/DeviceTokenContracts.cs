namespace UTTTGO.Api.Modules.Notifications.Contracts;

public sealed record RegisterDeviceTokenRequest(string? Token, string? InstallationId, string? Platform,
    string? AppVersion);
public sealed record DeviceTokenResponse(string DeviceTokenId, string InstallationId, string Platform,
    bool Active, DateTime UpdatedAt);
