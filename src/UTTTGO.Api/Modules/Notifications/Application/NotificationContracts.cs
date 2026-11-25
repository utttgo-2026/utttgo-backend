namespace UTTTGO.Api.Modules.Notifications.Application;

public sealed record NotificationMessage(string EventType, string RecipientUserId, string Title, string Body,
    IReadOnlyDictionary<string, string> Data, string CorrelationId, string EventId);
public enum NotificationSendStatus { Sent, Disabled, TransientFailure, Unregistered, Invalid }
public sealed record NotificationSendResult(NotificationSendStatus Status);

public interface INotificationSender
{
    Task<NotificationSendResult> SendAsync(NotificationMessage message, string token,
        CancellationToken cancellationToken);
}

public interface INotificationEventPublisher
{
    Task PublishAsync(string eventType, string recipientUserId, string orderId, string status,
        string correlationId, string idempotencyKey, CancellationToken cancellationToken);
}

public interface IDeviceTokenStore
{
    Task<DeviceTokenRegistration> UpsertAsync(string userId, string token, string installationId,
        string platform, string? appVersion, CancellationToken cancellationToken);
    Task<bool> RemoveAsync(string userId, string deviceTokenId, CancellationToken cancellationToken);
    Task RemoveForUserAsync(string userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<DeviceTokenRecord>> FindActiveAsync(string userId, CancellationToken cancellationToken);
    Task DeactivateAsync(string deviceTokenId, CancellationToken cancellationToken);
    string Unprotect(string protectedToken);
}
public sealed record DeviceTokenRegistration(string Id, string InstallationId, string Platform, bool Created,
    DateTime UpdatedAt);
public sealed record DeviceTokenRecord(string Id, string UserId, string ProtectedToken, string TokenHash);
