namespace UTTTGO.Api.Modules.Orders.Application;

// Orders owns the orders collection and never reads the collections of other modules.
public interface IOrderRepository
{
    Task<OrderRecord> CreateAsync(OrderRecord order, CancellationToken cancellationToken);
    Task<OrderRecord?> FindByIdAsync(string orderId, CancellationToken cancellationToken);
    Task<OrderRecord?> FindByIdempotencyKeyAsync(string buyerUserId, string idempotencyKey,
        CancellationToken cancellationToken);
    Task<OrderPage> ListForBuyerAsync(string buyerUserId, string? status, int page, int pageSize,
        CancellationToken cancellationToken);
    Task<OrderPage> ListForSellerAsync(string sellerId, string? status, int page, int pageSize,
        CancellationToken cancellationToken);
    Task<OrderRecord?> ApplyAsync(string orderId, string expectedStatus, long expectedVersion,
        string? expectedDeliveryTokenHash, OrderMutation mutation, CancellationToken cancellationToken);
}

public interface IActiveOrderReader
{
    Task<bool> HasActiveOrdersAsync(string buyerUserId, string? sellerId, CancellationToken cancellationToken);
}

// Published read contract for Reviews. Reviews never reads the orders collection.
public interface IDeliveredOrderReader
{
    Task<DeliveredOrderSnapshot?> FindDeliveredForBuyerAsync(string orderId, string buyerUserId,
        CancellationToken cancellationToken);
}

public sealed record DeliveredOrderSnapshot(string OrderId, string BuyerUserId, string SellerId,
    string ProductId, string Status, DateTime? DeliveredAt);

// Every write to an order goes through one compare-and-set: the stored status and the
// stored version must match the ones the caller read, otherwise nothing is changed.
public sealed record OrderMutation(string? NewStatus = null, DateTime? DeliveredAt = null,
    DateTime? CompletedAt = null, string? DeliveryTokenHash = null,
    DateTime? DeliveryTokenExpiresAt = null, DateTime? DeliveryTokenUsedAt = null,
    bool ClearDeliveryTokenUsedAt = false);

public sealed record OrderRecord(string OrderId, string BuyerUserId, string SellerId, string ProductId,
    string ProductName, int Quantity, decimal UnitPrice, decimal Total, string Currency, string PaymentMethod,
    string Status, string ReservationId, string PickupPoint, DateTime? DeliveredAt, DateTime? CompletedAt,
    long Version, string? IdempotencyKey, DateTime CreatedAt, DateTime UpdatedAt, string? Notes = null,
    string? DeliveryTokenHash = null, DateTime? DeliveryTokenExpiresAt = null,
    DateTime? DeliveryTokenUsedAt = null);

public sealed record OrderPage(IReadOnlyList<OrderRecord> Items, long Total);

// Raised when a repeated Idempotency-Key collides with an existing order.
public sealed class DuplicateOrderException : Exception
{
    public DuplicateOrderException() : base("Duplicate order.") { }
}

// Immutable trail of every state change (TASK-ORD-011).
public interface IOrderAuditWriter
{
    Task WriteAsync(OrderAuditRecord audit, CancellationToken cancellationToken);
}

public sealed record OrderAuditRecord(string OrderId, string ActorUserId, string ActorRole, string Action,
    string? FromStatus, string? ToStatus, string CorrelationId, DateTime OccurredAt);

// Distributed limits, partitioned by the authenticated user id and always fail-closed.
public interface IOrdersRateLimitGate
{
    Task<bool> IsAllowedForCreateAsync(string userId, CancellationToken cancellationToken);
    Task<bool> IsAllowedForReadAsync(string userId, CancellationToken cancellationToken);
    Task<bool> IsAllowedForWriteAsync(string userId, CancellationToken cancellationToken);
    Task<bool> IsAllowedForCancelAsync(string userId, CancellationToken cancellationToken);
    Task<bool> IsAllowedForDeliveryTokenAsync(string userId, CancellationToken cancellationToken);
    Task<bool> IsAllowedForDeliveryConfirmAsync(string userId, CancellationToken cancellationToken);
}
