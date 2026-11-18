using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Orders.Contracts;
using UTTTGO.Api.Shared.Contracts;

namespace UTTTGO.Api.Modules.Orders.Application;

public interface IListSellerOrdersService
{
    Task<ListSellerOrdersResult> ListAsync(string token, OrderListQuery query,
        CancellationToken cancellationToken);
}

public enum ListSellerOrdersOutcome
{
    Success,
    Unauthorized,
    AccountNotActive,
    NotSeller,
    RateLimited,
    Unavailable
}

public sealed record ListSellerOrdersResult(ListSellerOrdersOutcome Outcome,
    OrderListResponse? Response = null);

public sealed class ListSellerOrdersService(IAuthenticatedLocalProfileReader profiles,
    IOrdersRateLimitGate rateLimit, IOrderRepository orders) : IListSellerOrdersService
{
    public async Task<ListSellerOrdersResult> ListAsync(string token, OrderListQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            var authorization = await profiles.ReadAsync(token, cancellationToken);
            if (authorization.Outcome is AuthenticatedLocalProfileOutcome.Unauthorized
                or AuthenticatedLocalProfileOutcome.ProfileMissing)
                return new(ListSellerOrdersOutcome.Unauthorized);
            if (authorization.Outcome != AuthenticatedLocalProfileOutcome.Success
                || authorization.Profile is null)
                return new(ListSellerOrdersOutcome.Unavailable);

            var profile = authorization.Profile;
            if (!string.Equals(profile.AccountStatus, "Active", StringComparison.Ordinal))
                return new(ListSellerOrdersOutcome.AccountNotActive);
            if (!profile.Roles.Contains("Seller", StringComparer.Ordinal) ||
                string.IsNullOrWhiteSpace(profile.SellerId))
                return new(ListSellerOrdersOutcome.NotSeller);

            if (!await rateLimit.IsAllowedForReadAsync(profile.Id, cancellationToken))
                return new(ListSellerOrdersOutcome.RateLimited);

            var page = await orders.ListForSellerAsync(profile.SellerId!, query.Status, query.Page,
                query.PageSize, cancellationToken);
            var items = page.Items
                .Select(order => new OrderSummaryResponse(order.OrderId, order.ProductId, order.ProductName,
                    order.SellerId, order.BuyerUserId, order.Quantity, order.UnitPrice, order.Total,
                    order.Currency, order.Status, "seller", order.CreatedAt, order.Notes))
                .ToList();
            return new(ListSellerOrdersOutcome.Success,
                new OrderListResponse(items, query.Page, query.PageSize, PaginationCount.FromCount(page.Total)));
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new(ListSellerOrdersOutcome.Unavailable);
        }
    }
}
