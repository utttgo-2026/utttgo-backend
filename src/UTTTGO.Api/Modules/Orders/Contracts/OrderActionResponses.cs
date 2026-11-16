namespace UTTTGO.Api.Modules.Orders.Contracts;

public sealed record OrderCancellationResponse(string OrderId, string Status, DateTime? CompletedAt,
    long Version);

public sealed record OrderStatusUpdateResponse(string OrderId, string Status, long Version,
    DateTime UpdatedAt);

public sealed record DeliveryTokenResponse(string OrderId, string Token, DateTime ExpiresAt);

public sealed record DeliveryConfirmationResponse(string OrderId, string Status, DateTime? DeliveredAt,
    long Version);
