using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Orders.Contracts;

namespace UTTTGO.Api.Modules.Orders.Application;

public interface IGetOrderService
{
    Task<GetOrderResult> GetAsync(string token, string orderId, CancellationToken cancellationToken);
}

public enum GetOrderOutcome { Found, Unauthorized, NotFound, RateLimited, Unavailable }

public sealed record GetOrderResult(GetOrderOutcome Outcome, OrderResponse? Response = null);

public sealed class GetOrderService(IAuthenticatedLocalProfileReader profiles, IOrdersRateLimitGate rateLimit,
    IOrderRepository orders) : IGetOrderService
{
    public async Task<GetOrderResult> GetAsync(string token, string orderId,
        CancellationToken cancellationToken)
    {
        try
        {
            var authorization = await profiles.ReadAsync(token, cancellationToken);
            if (authorization.Outcome is AuthenticatedLocalProfileOutcome.Unauthorized
                or AuthenticatedLocalProfileOutcome.ProfileMissing)
                return new(GetOrderOutcome.Unauthorized);
            if (authorization.Outcome != AuthenticatedLocalProfileOutcome.Success
                || authorization.Profile is null)
                return new(GetOrderOutcome.Unavailable);

            var profile = authorization.Profile;
            if (!await rateLimit.IsAllowedForReadAsync(profile.Id, cancellationToken))
                return new(GetOrderOutcome.RateLimited);

            var order = await orders.FindByIdAsync(orderId, cancellationToken);
            if (order is null) return new(GetOrderOutcome.NotFound);

            // Authorization by resource: only the buyer or the seller of that order may
            // read it. A third party gets 404 so the existence is never disclosed.
            var isBuyer = string.Equals(order.BuyerUserId, profile.Id, StringComparison.Ordinal);
            var isSeller = !string.IsNullOrWhiteSpace(profile.SellerId) &&
                string.Equals(order.SellerId, profile.SellerId, StringComparison.Ordinal);
            if (!isBuyer && !isSeller) return new(GetOrderOutcome.NotFound);

            return new(GetOrderOutcome.Found, new OrderResponse(order.OrderId, order.ProductId,
                order.ProductName, order.SellerId, order.BuyerUserId, order.Quantity, order.UnitPrice,
                order.Total, order.Currency, order.PaymentMethod, order.Status, order.PickupPoint,
                order.DeliveredAt, order.CompletedAt, order.CreatedAt, order.UpdatedAt, order.Version,
                order.Notes));
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new(GetOrderOutcome.Unavailable);
        }
    }
}
