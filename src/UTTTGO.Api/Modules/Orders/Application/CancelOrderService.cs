using UTTTGO.Api.Modules.Catalog.Application;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Orders.Contracts;

namespace UTTTGO.Api.Modules.Orders.Application;

public interface ICancelOrderService
{
    Task<CancelOrderResult> CancelAsync(string token, string orderId, string correlationId,
        CancellationToken cancellationToken);
}

public enum CancelOrderOutcome
{
    Cancelled,
    AlreadyCancelled,
    Unauthorized,
    AccountNotActive,
    Forbidden,
    NotFound,
    StateConflict,
    RateLimited,
    Unavailable
}

public sealed record CancelOrderResult(CancelOrderOutcome Outcome,
    OrderCancellationResponse? Response = null);

public sealed class CancelOrderService(IAuthenticatedLocalProfileReader profiles,
    IOrdersRateLimitGate rateLimit, IOrderRepository orders, ICatalogOrderProducts catalog,
    IOrderAuditWriter audit, ILogger<CancelOrderService> logger,
    UTTTGO.Api.Modules.Notifications.Application.INotificationEventPublisher? notifications = null) : ICancelOrderService
{
    public async Task<CancelOrderResult> CancelAsync(string token, string orderId, string correlationId,
        CancellationToken cancellationToken)
    {
        try
        {
            var authorization = await profiles.ReadAsync(token, cancellationToken);
            if (authorization.Outcome is AuthenticatedLocalProfileOutcome.Unauthorized
                or AuthenticatedLocalProfileOutcome.ProfileMissing)
                return new(CancelOrderOutcome.Unauthorized);
            if (authorization.Outcome != AuthenticatedLocalProfileOutcome.Success
                || authorization.Profile is null)
                return new(CancelOrderOutcome.Unavailable);

            var profile = authorization.Profile;
            if (!string.Equals(profile.AccountStatus, "Active", StringComparison.Ordinal))
                return new(CancelOrderOutcome.AccountNotActive);

            if (!await rateLimit.IsAllowedForCancelAsync(profile.Id, cancellationToken))
                return new(CancelOrderOutcome.RateLimited);

            var order = await orders.FindByIdAsync(orderId, cancellationToken);
            if (order is null) return new(CancelOrderOutcome.NotFound);

            var isBuyer = string.Equals(order.BuyerUserId, profile.Id, StringComparison.Ordinal);
            var isSeller = !string.IsNullOrWhiteSpace(profile.SellerId) &&
                string.Equals(order.SellerId, profile.SellerId, StringComparison.Ordinal);
            if (!isBuyer)
                return new(isSeller ? CancelOrderOutcome.Forbidden : CancelOrderOutcome.NotFound);

            var now = DateTime.UtcNow;
            var current = order;
            var transitioned = false;
            if (!string.Equals(order.Status, OrderStatus.CancelledByBuyer, StringComparison.Ordinal))
            {
                if (!string.Equals(order.Status, OrderStatus.Pending, StringComparison.Ordinal))
                    return new(CancelOrderOutcome.StateConflict);

                current = await orders.ApplyAsync(order.OrderId, OrderStatus.Pending, order.Version, null,
                    new OrderMutation(NewStatus: OrderStatus.CancelledByBuyer, CompletedAt: now),
                    cancellationToken);
                if (current is null) return new(CancelOrderOutcome.StateConflict);
                transitioned = true;
            }

            try
            {
                await catalog.ReleaseStockAsync(order.ProductId, order.Quantity, order.ReservationId,
                    cancellationToken);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogError("Order {OrderId} was cancelled but its stock release failed.", order.OrderId);
                return new(CancelOrderOutcome.Unavailable);
            }

            if (transitioned)
            {
                await WriteAuditAsync(current, profile.Id, "buyer", correlationId, cancellationToken);
                try
                {
                    if (notifications is not null)
                        await notifications.PublishAsync("order.status.changed", current.BuyerUserId, current.OrderId,
                            current.Status, correlationId, $"order.status.changed:{current.OrderId}:{current.Version}", cancellationToken);
                }
                catch (Exception) when (!cancellationToken.IsCancellationRequested)
                { logger.LogWarning("Notification outbox could not record cancellation {OrderId}.", current.OrderId); }
            }

            return new(transitioned ? CancelOrderOutcome.Cancelled : CancelOrderOutcome.AlreadyCancelled,
                new OrderCancellationResponse(current.OrderId, current.Status, current.CompletedAt,
                    current.Version));
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new(CancelOrderOutcome.Unavailable);
        }
    }

    private async Task WriteAuditAsync(OrderRecord order, string actorUserId, string actorRole,
        string correlationId, CancellationToken cancellationToken)
    {
        try
        {
            await audit.WriteAsync(new OrderAuditRecord(order.OrderId, actorUserId, actorRole, "Cancelled",
                OrderStatus.Pending, OrderStatus.CancelledByBuyer, correlationId, DateTime.UtcNow),
                cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError("Order {OrderId} was cancelled without its audit trail.", order.OrderId);
        }
    }
}
