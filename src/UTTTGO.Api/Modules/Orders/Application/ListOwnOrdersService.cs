using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Orders.Contracts;

namespace UTTTGO.Api.Modules.Orders.Application;

public interface IListOwnOrdersService
{
    Task<ListOwnOrdersResult> ListAsync(string token, OrderListQuery query,
        CancellationToken cancellationToken);
}

public enum ListOwnOrdersOutcome { Success, Unauthorized, RateLimited, Unavailable }

public sealed record ListOwnOrdersResult(ListOwnOrdersOutcome Outcome, OrderListResponse? Response = null);

public sealed class ListOwnOrdersService(IAuthenticatedLocalProfileReader profiles,
    IOrdersRateLimitGate rateLimit, IOrderRepository orders) : IListOwnOrdersService
{
    public async Task<ListOwnOrdersResult> ListAsync(string token, OrderListQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            var authorization = await profiles.ReadAsync(token, cancellationToken);
            if (authorization.Outcome is AuthenticatedLocalProfileOutcome.Unauthorized
                or AuthenticatedLocalProfileOutcome.ProfileMissing)
                return new(ListOwnOrdersOutcome.Unauthorized);
            if (authorization.Outcome != AuthenticatedLocalProfileOutcome.Success
                || authorization.Profile is null)
                return new(ListOwnOrdersOutcome.Unavailable);

            var profile = authorization.Profile;
            if (!await rateLimit.IsAllowedForReadAsync(profile.Id, cancellationToken))
                return new(ListOwnOrdersOutcome.RateLimited);

            var page = await orders.ListForBuyerAsync(profile.Id, query.Status, query.Page, query.PageSize,
                cancellationToken);
            var items = page.Items
                .Select(order => new OrderSummaryResponse(order.OrderId, order.ProductId, order.ProductName,
                    order.Quantity, order.Total, order.Currency, order.Status, "buyer", order.CreatedAt))
                .ToList();
            return new(ListOwnOrdersOutcome.Success,
                new OrderListResponse(items, query.Page, query.PageSize, page.Total));
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new(ListOwnOrdersOutcome.Unavailable);
        }
    }
}
