using Microsoft.Extensions.Options;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Orders.Contracts;

namespace UTTTGO.Api.Modules.Orders.Application;

public interface ICreateDeliveryTokenService
{
    Task<CreateDeliveryTokenResult> CreateAsync(string token, string orderId, string correlationId,
        CancellationToken cancellationToken);
}

public enum CreateDeliveryTokenOutcome
{
    Created,
    Unauthorized,
    AccountNotActive,
    Forbidden,
    NotFound,
    StateConflict,
    RateLimited,
    Unavailable
}

public sealed record CreateDeliveryTokenResult(CreateDeliveryTokenOutcome Outcome,
    DeliveryTokenResponse? Response = null);

public sealed class CreateDeliveryTokenService(IAuthenticatedLocalProfileReader profiles,
    IOrdersRateLimitGate rateLimit, IOrderRepository orders, IOptions<OrderLimitsOptions> limits,
    IOrderAuditWriter audit, ILogger<CreateDeliveryTokenService> logger) : ICreateDeliveryTokenService
{
    public async Task<CreateDeliveryTokenResult> CreateAsync(string token, string orderId,
        string correlationId, CancellationToken cancellationToken)
    {
        try
        {
            var authorization = await profiles.ReadAsync(token, cancellationToken);
            if (authorization.Outcome is AuthenticatedLocalProfileOutcome.Unauthorized
                or AuthenticatedLocalProfileOutcome.ProfileMissing)
                return new(CreateDeliveryTokenOutcome.Unauthorized);
            if (authorization.Outcome != AuthenticatedLocalProfileOutcome.Success
                || authorization.Profile is null)
                return new(CreateDeliveryTokenOutcome.Unavailable);

            var profile = authorization.Profile;
            if (!string.Equals(profile.AccountStatus, "Active", StringComparison.Ordinal))
                return new(CreateDeliveryTokenOutcome.AccountNotActive);

            if (!await rateLimit.IsAllowedForDeliveryTokenAsync(profile.Id, cancellationToken))
                return new(CreateDeliveryTokenOutcome.RateLimited);

            var order = await orders.FindByIdAsync(orderId, cancellationToken);
            if (order is null) return new(CreateDeliveryTokenOutcome.NotFound);

            var isBuyer = string.Equals(order.BuyerUserId, profile.Id, StringComparison.Ordinal);
            var isSeller = !string.IsNullOrWhiteSpace(profile.SellerId) &&
                string.Equals(order.SellerId, profile.SellerId, StringComparison.Ordinal);
            if (!isBuyer)
                return new(isSeller ? CreateDeliveryTokenOutcome.Forbidden : CreateDeliveryTokenOutcome.NotFound);

            if (!string.Equals(order.Status, OrderStatus.Ready, StringComparison.Ordinal))
                return new(CreateDeliveryTokenOutcome.StateConflict);

            var tokenValue = OrderDeliveryToken.Generate();
            var hash = OrderDeliveryToken.Hash(tokenValue);
            var now = DateTime.UtcNow;
            var expiresAt = now.AddSeconds(Math.Max(60, limits.Value.DeliveryTokenTtlSeconds));

            var updated = await orders.ApplyAsync(order.OrderId, OrderStatus.Ready, order.Version, null,
                new OrderMutation(DeliveryTokenHash: hash, DeliveryTokenExpiresAt: expiresAt,
                    ClearDeliveryTokenUsedAt: true), cancellationToken);
            if (updated is null)
            {
                var current = await orders.FindByIdAsync(order.OrderId, cancellationToken);
                if (current is null) return new(CreateDeliveryTokenOutcome.NotFound);
                return new(CreateDeliveryTokenOutcome.StateConflict);
            }

            await WriteAuditAsync(updated, profile.Id, correlationId, cancellationToken);

            return new(CreateDeliveryTokenOutcome.Created,
                new DeliveryTokenResponse(updated.OrderId, tokenValue, expiresAt));
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new(CreateDeliveryTokenOutcome.Unavailable);
        }
    }

    private async Task WriteAuditAsync(OrderRecord order, string actorUserId, string correlationId,
        CancellationToken cancellationToken)
    {
        try
        {
            await audit.WriteAsync(new OrderAuditRecord(order.OrderId, actorUserId, "buyer",
                "DeliveryTokenIssued", OrderStatus.Ready, OrderStatus.Ready, correlationId, DateTime.UtcNow),
                cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError("Order {OrderId} issued a delivery token without its audit trail.",
                order.OrderId);
        }
    }
}
