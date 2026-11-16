using UTTTGO.Api.Modules.Catalog.Application;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Orders.Contracts;

namespace UTTTGO.Api.Modules.Orders.Application;

public interface IConfirmDeliveryService
{
    Task<ConfirmDeliveryResult> ConfirmAsync(string token, string orderId,
        DeliveryConfirmationRequest request, string correlationId, CancellationToken cancellationToken);
}

public enum ConfirmDeliveryOutcome
{
    Delivered,
    Unauthorized,
    AccountNotActive,
    Forbidden,
    NotFound,
    StateConflict,
    TokenInvalid,
    TokenExpired,
    TokenAlreadyUsed,
    RateLimited,
    Unavailable
}

public sealed record ConfirmDeliveryResult(ConfirmDeliveryOutcome Outcome,
    DeliveryConfirmationResponse? Response = null);

public sealed class ConfirmDeliveryService(IAuthenticatedLocalProfileReader profiles,
    IOrdersRateLimitGate rateLimit, IOrderRepository orders, ICatalogOrderProducts catalog,
    IOrderAuditWriter audit, ILogger<ConfirmDeliveryService> logger) : IConfirmDeliveryService
{
    public async Task<ConfirmDeliveryResult> ConfirmAsync(string token, string orderId,
        DeliveryConfirmationRequest request, string correlationId, CancellationToken cancellationToken)
    {
        try
        {
            var authorization = await profiles.ReadAsync(token, cancellationToken);
            if (authorization.Outcome is AuthenticatedLocalProfileOutcome.Unauthorized
                or AuthenticatedLocalProfileOutcome.ProfileMissing)
                return new(ConfirmDeliveryOutcome.Unauthorized);
            if (authorization.Outcome != AuthenticatedLocalProfileOutcome.Success
                || authorization.Profile is null)
                return new(ConfirmDeliveryOutcome.Unavailable);

            var profile = authorization.Profile;
            if (!string.Equals(profile.AccountStatus, "Active", StringComparison.Ordinal))
                return new(ConfirmDeliveryOutcome.AccountNotActive);

            if (!await rateLimit.IsAllowedForDeliveryConfirmAsync(profile.Id, cancellationToken))
                return new(ConfirmDeliveryOutcome.RateLimited);

            var order = await orders.FindByIdAsync(orderId, cancellationToken);
            if (order is null) return new(ConfirmDeliveryOutcome.NotFound);

            var isBuyer = string.Equals(order.BuyerUserId, profile.Id, StringComparison.Ordinal);
            var isSeller = !string.IsNullOrWhiteSpace(profile.SellerId) &&
                string.Equals(order.SellerId, profile.SellerId, StringComparison.Ordinal);
            if (!isSeller)
                return new(isBuyer ? ConfirmDeliveryOutcome.Forbidden : ConfirmDeliveryOutcome.NotFound);

            if (!string.Equals(order.Status, OrderStatus.Ready, StringComparison.Ordinal))
                return new(ConfirmDeliveryOutcome.StateConflict);

            var tokenValue = request.Token?.Trim();
            if (string.IsNullOrWhiteSpace(tokenValue)) return new(ConfirmDeliveryOutcome.TokenInvalid);
            if (order.DeliveryTokenUsedAt is not null) return new(ConfirmDeliveryOutcome.TokenAlreadyUsed);
            if (string.IsNullOrWhiteSpace(order.DeliveryTokenHash))
                return new(ConfirmDeliveryOutcome.TokenInvalid);
            if (order.DeliveryTokenExpiresAt is null || order.DeliveryTokenExpiresAt <= DateTime.UtcNow)
                return new(ConfirmDeliveryOutcome.TokenExpired);
            if (!OrderDeliveryToken.Matches(tokenValue, order.DeliveryTokenHash))
                return new(ConfirmDeliveryOutcome.TokenInvalid);

            var now = DateTime.UtcNow;
            var updated = await orders.ApplyAsync(order.OrderId, OrderStatus.Ready, order.Version,
                order.DeliveryTokenHash,
                new OrderMutation(NewStatus: OrderStatus.Delivered, DeliveredAt: now, CompletedAt: now,
                    DeliveryTokenUsedAt: now), cancellationToken);
            if (updated is null)
                return new(await ConflictOutcomeAsync(order, cancellationToken));

            try
            {
                await catalog.CommitStockAsync(order.ProductId, order.ReservationId, cancellationToken);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogError("Order {OrderId} was delivered but its stock commit failed.", order.OrderId);
            }

            await WriteAuditAsync(updated, profile.Id, correlationId, cancellationToken);

            return new(ConfirmDeliveryOutcome.Delivered, new DeliveryConfirmationResponse(updated.OrderId,
                updated.Status, updated.DeliveredAt, updated.Version));
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new(ConfirmDeliveryOutcome.Unavailable);
        }
    }

    private async Task<ConfirmDeliveryOutcome> ConflictOutcomeAsync(OrderRecord order,
        CancellationToken cancellationToken)
    {
        var current = await orders.FindByIdAsync(order.OrderId, cancellationToken);
        if (current is null) return ConfirmDeliveryOutcome.NotFound;
        if (string.Equals(current.Status, OrderStatus.Delivered, StringComparison.Ordinal))
            return ConfirmDeliveryOutcome.TokenAlreadyUsed;
        if (!string.Equals(current.Status, OrderStatus.Ready, StringComparison.Ordinal))
            return ConfirmDeliveryOutcome.StateConflict;
        return ConfirmDeliveryOutcome.TokenInvalid;
    }

    private async Task WriteAuditAsync(OrderRecord order, string actorUserId, string correlationId,
        CancellationToken cancellationToken)
    {
        try
        {
            await audit.WriteAsync(new OrderAuditRecord(order.OrderId, actorUserId, "seller",
                "DeliveryConfirmed", OrderStatus.Ready, OrderStatus.Delivered, correlationId, DateTime.UtcNow),
                cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError("Order {OrderId} was delivered without its audit trail.", order.OrderId);
        }
    }
}
