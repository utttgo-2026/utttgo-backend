using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Notifications.Contracts;

namespace UTTTGO.Api.Modules.Notifications.Application;

public interface INotificationsRateLimitGate { Task<bool> IsAllowedAsync(string userId, CancellationToken ct); }
public sealed class DeviceTokenService(IAuthenticatedLocalProfileReader profiles, IDeviceTokenStore tokens,
    INotificationsRateLimitGate rateLimit) : IDeviceTokenService
{
    public async Task<DeviceTokenOperationResult> RegisterAsync(string bearer, RegisterDeviceTokenRequest request, CancellationToken ct)
    {
        try
        {
            var auth = await profiles.ReadAsync(bearer, ct);
            if (auth.Outcome is AuthenticatedLocalProfileOutcome.Unauthorized or AuthenticatedLocalProfileOutcome.ProfileMissing) return new(DeviceTokenOutcome.Unauthorized);
            if (auth.Outcome != AuthenticatedLocalProfileOutcome.Success || auth.Profile is null) return new(DeviceTokenOutcome.Unavailable);
            if (auth.Profile.AccountStatus != "Active") return new(DeviceTokenOutcome.NotActive);
            if (!await rateLimit.IsAllowedAsync(auth.Profile.Id, ct)) return new(DeviceTokenOutcome.RateLimited);
            var result = await tokens.UpsertAsync(auth.Profile.Id, request.Token!.Trim(), request.InstallationId!.Trim(), request.Platform!, request.AppVersion?.Trim(), ct);
            return new(result.Created ? DeviceTokenOutcome.Created : DeviceTokenOutcome.Updated,
                new(result.Id, result.InstallationId, result.Platform, true, result.UpdatedAt));
        }
        catch (Exception) when (!ct.IsCancellationRequested) { return new(DeviceTokenOutcome.Unavailable); }
    }
    public async Task<DeviceTokenOutcome> RemoveAsync(string bearer, string id, CancellationToken ct)
    {
        try
        {
            var auth = await profiles.ReadAsync(bearer, ct);
            if (auth.Outcome is AuthenticatedLocalProfileOutcome.Unauthorized or AuthenticatedLocalProfileOutcome.ProfileMissing) return DeviceTokenOutcome.Unauthorized;
            if (auth.Outcome != AuthenticatedLocalProfileOutcome.Success || auth.Profile is null) return DeviceTokenOutcome.Unavailable;
            if (auth.Profile.AccountStatus != "Active") return DeviceTokenOutcome.NotActive;
            if (!await tokens.RemoveAsync(auth.Profile.Id, id, ct)) return DeviceTokenOutcome.NotFound;
            return DeviceTokenOutcome.Removed;
        }
        catch (Exception) when (!ct.IsCancellationRequested) { return DeviceTokenOutcome.Unavailable; }
    }
}
public interface IDeviceTokenService
{
    Task<DeviceTokenOperationResult> RegisterAsync(string bearer, RegisterDeviceTokenRequest request, CancellationToken ct);
    Task<DeviceTokenOutcome> RemoveAsync(string bearer, string id, CancellationToken ct);
}
public enum DeviceTokenOutcome { Created, Updated, Removed, Unauthorized, NotActive, NotFound, RateLimited, Unavailable }
public sealed record DeviceTokenOperationResult(DeviceTokenOutcome Outcome, DeviceTokenResponse? Response = null);
