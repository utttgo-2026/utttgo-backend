using UTTTGO.Api.Modules.Catalog.Application;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Orders.Application;
using UTTTGO.Api.Modules.Sellers.Application;

namespace UtttGo.SecurityTests;

internal sealed class OrdersRepositoryDouble : IOrderRepository
{
    public Dictionary<string, OrderRecord> Orders { get; } = new(StringComparer.Ordinal);
    public int CreateCalls { get; private set; }
    public bool Fail { get; set; }

    public Task<OrderRecord> CreateAsync(OrderRecord order, CancellationToken cancellationToken)
    {
        if (Fail) throw new InvalidOperationException("internal-secret");
        CreateCalls++;
        var created = order with { OrderId = OrderFixtures.OrderId };
        Orders[created.OrderId] = created;
        return Task.FromResult(created);
    }

    public Task<OrderRecord?> FindByIdAsync(string orderId, CancellationToken cancellationToken)
    {
        if (Fail) throw new InvalidOperationException("internal-secret");
        Orders.TryGetValue(orderId, out var order);
        return Task.FromResult(order);
    }

    public Task<OrderRecord?> FindByIdempotencyKeyAsync(string buyerUserId, string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (Fail) throw new InvalidOperationException("internal-secret");
        var order = Orders.Values.FirstOrDefault(item => item.BuyerUserId == buyerUserId &&
            item.IdempotencyKey == idempotencyKey);
        return Task.FromResult(order);
    }

    public Task<OrderPage> ListForBuyerAsync(string buyerUserId, string? status, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        if (Fail) throw new InvalidOperationException("internal-secret");
        var items = Orders.Values
            .Where(order => order.BuyerUserId == buyerUserId)
            .Where(order => status is null || order.Status == status)
            .OrderByDescending(order => order.CreatedAt)
            .ToList();
        return Task.FromResult(new OrderPage(items, items.Count));
    }

    public Task<OrderPage> ListForSellerAsync(string sellerId, string? status, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        if (Fail) throw new InvalidOperationException("internal-secret");
        var items = Orders.Values
            .Where(order => order.SellerId == sellerId)
            .Where(order => status is null || order.Status == status)
            .OrderByDescending(order => order.CreatedAt)
            .ToList();
        return Task.FromResult(new OrderPage(items, items.Count));
    }

    public Task<OrderRecord?> ApplyAsync(string orderId, string expectedStatus, long expectedVersion,
        string? expectedDeliveryTokenHash, OrderMutation mutation, CancellationToken cancellationToken)
    {
        if (Fail) throw new InvalidOperationException("internal-secret");
        if (!Orders.TryGetValue(orderId, out var order)) return Task.FromResult<OrderRecord?>(null);
        if (order.Status != expectedStatus || order.Version != expectedVersion)
            return Task.FromResult<OrderRecord?>(null);
        if (expectedDeliveryTokenHash is not null && order.DeliveryTokenHash != expectedDeliveryTokenHash)
            return Task.FromResult<OrderRecord?>(null);

        var updated = order with
        {
            Status = mutation.NewStatus ?? order.Status,
            DeliveredAt = mutation.DeliveredAt ?? order.DeliveredAt,
            CompletedAt = mutation.CompletedAt ?? order.CompletedAt,
            DeliveryTokenHash = mutation.DeliveryTokenHash ?? order.DeliveryTokenHash,
            DeliveryTokenExpiresAt = mutation.DeliveryTokenExpiresAt ?? order.DeliveryTokenExpiresAt,
            DeliveryTokenUsedAt = mutation.ClearDeliveryTokenUsedAt
                ? null
                : mutation.DeliveryTokenUsedAt ?? order.DeliveryTokenUsedAt,
            Version = order.Version + 1,
            UpdatedAt = DateTime.UtcNow
        };
        Orders[orderId] = updated;
        return Task.FromResult<OrderRecord?>(updated);
    }
}

internal sealed class OrdersAuditWriterDouble : IOrderAuditWriter
{
    public List<OrderAuditRecord> Records { get; } = [];

    public Task WriteAsync(OrderAuditRecord audit, CancellationToken cancellationToken)
    {
        Records.Add(audit);
        return Task.CompletedTask;
    }
}

internal sealed class OrdersRateLimitDouble : IOrdersRateLimitGate
{
    public bool Allowed { get; set; } = true;
    public bool Fail { get; set; }

    public Task<bool> IsAllowedForCreateAsync(string userId, CancellationToken cancellationToken) => Check();

    public Task<bool> IsAllowedForReadAsync(string userId, CancellationToken cancellationToken) => Check();

    public Task<bool> IsAllowedForWriteAsync(string userId, CancellationToken cancellationToken) => Check();

    public Task<bool> IsAllowedForCancelAsync(string userId, CancellationToken cancellationToken) => Check();

    public Task<bool> IsAllowedForDeliveryTokenAsync(string userId, CancellationToken cancellationToken) => Check();

    public Task<bool> IsAllowedForDeliveryConfirmAsync(string userId, CancellationToken cancellationToken) => Check();

    private Task<bool> Check() =>
        Fail
            ? throw new InvalidOperationException("internal-secret")
            : Task.FromResult(Allowed);
}

internal sealed class CatalogOrderProductsDouble : ICatalogOrderProducts
{
    public CatalogOrderProduct? Product { get; set; }
    public CatalogStockOutcome ReserveOutcome { get; set; } = CatalogStockOutcome.Reserved;
    public int ReserveCalls { get; private set; }
    public int ReleaseCalls { get; private set; }
    public int CommitCalls { get; private set; }
    public bool Fail { get; set; }

    public Task<CatalogOrderProduct?> FindForOrderAsync(string productId,
        CancellationToken cancellationToken) =>
        Fail
            ? throw new InvalidOperationException("internal-secret")
            : Task.FromResult(Product);

    public Task<CatalogStockOutcome> ReserveStockAsync(string productId, int quantity, string reservationId,
        CancellationToken cancellationToken)
    {
        if (Fail) throw new InvalidOperationException("internal-secret");
        ReserveCalls++;
        return Task.FromResult(ReserveOutcome);
    }

    public Task<bool> ReleaseStockAsync(string productId, int quantity, string reservationId,
        CancellationToken cancellationToken)
    {
        ReleaseCalls++;
        return Task.FromResult(true);
    }

    public Task<bool> CommitStockAsync(string productId, string reservationId,
        CancellationToken cancellationToken)
    {
        CommitCalls++;
        return Task.FromResult(true);
    }
}

internal sealed class SellerOrderProfilesDouble : ISellerOrderProfiles
{
    public SellerOrderProfile? Profile { get; set; }
    public bool Fail { get; set; }

    public Task<SellerOrderProfile?> FindOrderProfileAsync(string sellerId,
        CancellationToken cancellationToken) =>
        Fail
            ? throw new InvalidOperationException("internal-secret")
            : Task.FromResult(Profile);
}

internal static class OrderFixtures
{
    public const string ProductId = "665f1c2e8b3f2a0012ab34cd";
    public const string SellerId = "665f1a108b3f2a0012ab34c1";
    public const string OrderId = "665f1d408b3f2a0012ab34ce";
    public const string OtherBuyerId = "other-local-id";
    public const string SellerUserId = "local-seller-id";

    public static CatalogOrderProduct Product(bool published = true, decimal price = 85.50m, int stock = 5) =>
        new(ProductId, SellerId, "Quesadillas de huitlacoche (6 pza)", price, stock, published);

    public static SellerOrderProfile Seller(string status = "Active") =>
        new(SellerId, status, "Biblioteca central, entrada norte");

    public static OrderRecord Order(string buyerUserId = SellerFixtures.UserId, string sellerId = SellerId,
        string status = "Pending", long version = 0) =>
        new(OrderId, buyerUserId, sellerId, ProductId, "Quesadillas de huitlacoche (6 pza)", 2, 85.50m,
            171.00m, "MXN", "Cash", status, "reservation-1", "Biblioteca central, entrada norte", null,
            null, version, null, new DateTime(2026, 11, 16, 18, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 16, 18, 0, 0, DateTimeKind.Utc));

    public static LocalLoginProfile SellerActor(string status = "Active", string? sellerId = null) =>
        new(SellerUserId, "sub-seller", "vendedor@uttt.edu.mx", status, ["Buyer", "Seller"], "Vendedor",
            null, null, 1, sellerId ?? SellerId);
}
