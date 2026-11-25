using UTTTGO.Api.Modules.Catalog.Application;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Notifications.Application;
using UTTTGO.Api.Modules.Orders.Contracts;
using UTTTGO.Api.Modules.Sellers.Application;

namespace UTTTGO.Api.Modules.Orders.Application;

public interface ICreateOrderService
{
    Task<CreateOrderResult> CreateAsync(string token, CreateOrderRequest request, string? idempotencyKey,
        string correlationId, CancellationToken cancellationToken);
}

public enum CreateOrderOutcome
{
    Created,
    AlreadyCreated,
    IdempotencyKeyReused,
    Unauthorized,
    AccountNotActive,
    ProductNotFound,
    ProductNotAvailable,
    InsufficientStock,
    RateLimited,
    Unavailable
}

public sealed record CreateOrderResult(CreateOrderOutcome Outcome, OrderResponse? Response = null);

public sealed class CreateOrderService(IAuthenticatedLocalProfileReader profiles,
    IOrdersRateLimitGate rateLimit, ICatalogOrderProducts catalog, ISellerOrderProfiles sellers,
    IOrderRepository orders, IOrderAuditWriter audit, ILogger<CreateOrderService> logger,
    INotificationEventPublisher? notifications = null)
    : ICreateOrderService
{
    public async Task<CreateOrderResult> CreateAsync(string token, CreateOrderRequest request,
        string? idempotencyKey, string correlationId, CancellationToken cancellationToken)
    {
        try
        {
            var authorization = await profiles.ReadAsync(token, cancellationToken);
            if (authorization.Outcome is AuthenticatedLocalProfileOutcome.Unauthorized
                or AuthenticatedLocalProfileOutcome.ProfileMissing)
                return new(CreateOrderOutcome.Unauthorized);
            if (authorization.Outcome != AuthenticatedLocalProfileOutcome.Success
                || authorization.Profile is null)
                return new(CreateOrderOutcome.Unavailable);

            var profile = authorization.Profile;
            if (!string.Equals(profile.AccountStatus, "Active", StringComparison.Ordinal))
                return new(CreateOrderOutcome.AccountNotActive);

            if (!await rateLimit.IsAllowedForCreateAsync(profile.Id, cancellationToken))
                return new(CreateOrderOutcome.RateLimited);

            var key = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
            if (key is not null &&
                await orders.FindByIdempotencyKeyAsync(profile.Id, key, cancellationToken) is { } replay)
                return IsSameRequest(replay, request)
                    ? new(CreateOrderOutcome.AlreadyCreated, ToResponse(replay))
                    : new(CreateOrderOutcome.IdempotencyKeyReused);

            var product = await catalog.FindForOrderAsync(request.ProductId!, cancellationToken);
            if (product is null) return new(CreateOrderOutcome.ProductNotFound);
            if (!product.IsPublished) return new(CreateOrderOutcome.ProductNotAvailable);

            var seller = await sellers.FindOrderProfileAsync(product.SellerId, cancellationToken);
            if (seller is null || !string.Equals(seller.Status, "Active", StringComparison.Ordinal))
                return new(CreateOrderOutcome.ProductNotAvailable);

            var quantity = request.Quantity!.Value;
            var reservationId = Guid.NewGuid().ToString("N");
            var reservation = await catalog.ReserveStockAsync(product.ProductId, quantity, reservationId,
                cancellationToken);
            if (reservation == CatalogStockOutcome.InsufficientStock)
                return new(CreateOrderOutcome.InsufficientStock);
            if (reservation != CatalogStockOutcome.Reserved)
                return new(CreateOrderOutcome.ProductNotAvailable);

            var now = DateTime.UtcNow;
            var notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
            var record = new OrderRecord(string.Empty, profile.Id, product.SellerId, product.ProductId,
                product.Name, quantity, product.UnitPrice, product.UnitPrice * quantity, OrderStatus.CurrencyMxn,
                OrderStatus.PaymentMethodCash, OrderStatus.Pending, reservationId, seller.PickupPoint,
                null, null, 0, key, now, now, notes);

            OrderRecord created;
            try
            {
                created = await orders.CreateAsync(record, cancellationToken);
            }
            catch (DuplicateOrderException)
            {
                await catalog.ReleaseStockAsync(product.ProductId, quantity, reservationId, cancellationToken);
                var winner = key is null
                    ? null
                    : await orders.FindByIdempotencyKeyAsync(profile.Id, key, cancellationToken);
                return winner is null
                    ? new(CreateOrderOutcome.Unavailable)
                    : IsSameRequest(winner, request)
                        ? new(CreateOrderOutcome.AlreadyCreated, ToResponse(winner))
                        : new(CreateOrderOutcome.IdempotencyKeyReused);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                await catalog.ReleaseStockAsync(product.ProductId, quantity, reservationId, cancellationToken);
                return new(CreateOrderOutcome.Unavailable);
            }

            try
            {
                if (notifications is not null && !string.IsNullOrWhiteSpace(seller.OwnerUserId))
                    await notifications.PublishAsync("order.created", seller.OwnerUserId, created.OrderId,
                        created.Status, correlationId,
                        NotificationEventKeys.For("order.created", created.OrderId, created.Status),
                        cancellationToken);
                else if (notifications is not null)
                    logger.LogWarning("Order notification recipient could not be resolved.");
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            { logger.LogWarning("Notification outbox could not record order creation {OrderId}.", created.OrderId); }

            try
            {
                await audit.WriteAsync(new OrderAuditRecord(created.OrderId, profile.Id, "buyer", "Created",
                    null, OrderStatus.Pending, correlationId, now), cancellationToken);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogError("Order {OrderId} was created without its audit trail.", created.OrderId);
            }

            return new(CreateOrderOutcome.Created, ToResponse(created));
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new(CreateOrderOutcome.Unavailable);
        }
    }

    private static OrderResponse ToResponse(OrderRecord order) =>
        new(order.OrderId, order.ProductId, order.ProductName, order.SellerId, order.BuyerUserId,
            order.Quantity, order.UnitPrice, order.Total, order.Currency, order.PaymentMethod, order.Status,
            order.PickupPoint, order.DeliveredAt, order.CompletedAt, order.CreatedAt, order.UpdatedAt,
            order.Version, order.Notes);

    private static bool IsSameRequest(OrderRecord order, CreateOrderRequest request) =>
        string.Equals(order.ProductId, request.ProductId, StringComparison.Ordinal)
        && order.Quantity == request.Quantity
        && string.Equals(NormalizeNotes(order.Notes), NormalizeNotes(request.Notes), StringComparison.Ordinal);

    private static string? NormalizeNotes(string? notes) =>
        string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
}
