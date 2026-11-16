using UTTTGO.Api.Modules.Catalog.Application;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Orders.Contracts;

namespace UTTTGO.Api.Modules.Orders.Application;

public interface IUpdateOrderStatusService
{
    Task<UpdateOrderStatusResult> UpdateAsync(string token, string orderId,
        UpdateOrderStatusRequest request, string correlationId, CancellationToken cancellationToken);
}

public enum UpdateOrderStatusOutcome
{
    Updated,
    Unauthorized,
    AccountNotActive,
    Forbidden,
    NotFound,
    StateConflict,
    VersionConflict,
    RateLimited,
    Unavailable
}

public sealed record UpdateOrderStatusResult(UpdateOrderStatusOutcome Outcome,
    OrderStatusUpdateResponse? Response = null);

public sealed class UpdateOrderStatusService(IAuthenticatedLocalProfileReader profiles,
    IOrdersRateLimitGate rateLimit, IOrderRepository orders, ICatalogOrderProducts catalog,
    IOrderAuditWriter audit, ILogger<UpdateOrderStatusService> logger) : IUpdateOrderStatusService
{
    public async Task<UpdateOrderStatusResult> UpdateAsync(string token, string orderId,
        UpdateOrderStatusRequest request, string correlationId, CancellationToken cancellationToken)
    {
        try
        {
            var authorization = await profiles.ReadAsync(token, cancellationToken);
            if (authorization.Outcome is AuthenticatedLocalProfileOutcome.Unauthorized
                or AuthenticatedLocalProfileOutcome.ProfileMissing)
                return new(UpdateOrderStatusOutcome.Unauthorized);
            if (authorization.Outcome != AuthenticatedLocalProfileOutcome.Success
                || authorization.Profile is null)
                return new(UpdateOrderStatusOutcome.Unavailable);

            var profile = authorization.Profile;
            if (!string.Equals(profile.AccountStatus, "Active", StringComparison.Ordinal))
                return new(UpdateOrderStatusOutcome.AccountNotActive);

            if (!await rateLimit.IsAllowedForWriteAsync(profile.Id, cancellationToken))
                return new(UpdateOrderStatusOutcome.RateLimited);

            var order = await orders.FindByIdAsync(orderId, cancellationToken);
            if (order is null) return new(UpdateOrderStatusOutcome.NotFound);

            var isBuyer = string.Equals(order.BuyerUserId, profile.Id, StringComparison.Ordinal);
            var isSeller = !string.IsNullOrWhiteSpace(profile.SellerId) &&
                string.Equals(order.SellerId, profile.SellerId, StringComparison.Ordinal);
            if (!isSeller)
                return new(isBuyer ? UpdateOrderStatusOutcome.Forbidden : UpdateOrderStatusOutcome.NotFound);

            var target = request.Status!;
            if (target is OrderStatus.Delivered or OrderStatus.CancelledByBuyer)
                return new(UpdateOrderStatusOutcome.StateConflict);

            var version = request.Version!.Value;
            if (version != order.Version) return new(UpdateOrderStatusOutcome.VersionConflict);

            if (!OrderStatus.CanTransition(order.Status, target))
                return new(UpdateOrderStatusOutcome.StateConflict);

            var now = DateTime.UtcNow;
            var mutation = target == OrderStatus.Rejected
                ? new OrderMutation(NewStatus: target, CompletedAt: now)
                : new OrderMutation(NewStatus: target);
            var updated = await orders.ApplyAsync(order.OrderId, order.Status, version, null, mutation,
                cancellationToken);
            if (updated is null)
                return new(await ConflictOutcomeAsync(order, version, cancellationToken));

            if (target == OrderStatus.Rejected)
            {
                try
                {
                    await catalog.ReleaseStockAsync(order.ProductId, order.Quantity, order.ReservationId,
                        cancellationToken);
                }
                catch (Exception) when (!cancellationToken.IsCancellationRequested)
                {
                    logger.LogError("Order {OrderId} was rejected but its stock release failed.",
                        order.OrderId);
                    return new(UpdateOrderStatusOutcome.Unavailable);
                }
            }

            await WriteAuditAsync(updated, profile.Id, "seller", order.Status, target, correlationId,
                cancellationToken);

            return new(UpdateOrderStatusOutcome.Updated, new OrderStatusUpdateResponse(updated.OrderId,
                updated.Status, updated.Version, updated.UpdatedAt));
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new(UpdateOrderStatusOutcome.Unavailable);
        }
    }

    private async Task<UpdateOrderStatusOutcome> ConflictOutcomeAsync(OrderRecord order, long version,
        CancellationToken cancellationToken)
    {
        var current = await orders.FindByIdAsync(order.OrderId, cancellationToken);
        if (current is null) return UpdateOrderStatusOutcome.NotFound;
        if (current.Version != version) return UpdateOrderStatusOutcome.VersionConflict;
        return UpdateOrderStatusOutcome.StateConflict;
    }

    private async Task WriteAuditAsync(OrderRecord order, string actorUserId, string actorRole,
        string fromStatus, string toStatus, string correlationId, CancellationToken cancellationToken)
    {
        try
        {
            await audit.WriteAsync(new OrderAuditRecord(order.OrderId, actorUserId, actorRole,
                "StatusChanged", fromStatus, toStatus, correlationId, DateTime.UtcNow), cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError("Order {OrderId} changed status without its audit trail.", order.OrderId);
        }
    }
}
