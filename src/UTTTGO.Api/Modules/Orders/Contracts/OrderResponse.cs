namespace UTTTGO.Api.Modules.Orders.Contracts;

// Public projection of an order: only the commercial data of the purchase and the
// participants. Internal identifiers of other modules are never exposed.
public sealed record OrderResponse(string OrderId, string ProductId, string ProductName, string SellerId,
    string BuyerId, int Quantity, decimal UnitPrice, decimal TotalAmount, string Currency, string PaymentMethod,
    string Status, string PickupPoint, DateTime? DeliveredAt, DateTime? CompletedAt, DateTime CreatedAt,
    DateTime UpdatedAt, long Version, string? Notes = null);

public sealed record OrderSummaryResponse(string OrderId, string ProductId, string ProductName, string SellerId,
    string BuyerId, int Quantity, decimal UnitPrice, decimal TotalAmount, string Currency, string Status,
    string Role, DateTime CreatedAt, string? Notes = null);

public sealed record OrderListResponse(IReadOnlyList<OrderSummaryResponse> Items, int Page, int PageSize,
    int TotalCount);
