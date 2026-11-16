namespace UTTTGO.Api.Modules.Orders.Contracts;

public sealed record OrderListQuery(int Page, int PageSize, string? Status)
{
    public const int DefaultPageSize = 20;
    public const int MaxPage = 10_000;
    public const int MaxPageSize = 50;
}
