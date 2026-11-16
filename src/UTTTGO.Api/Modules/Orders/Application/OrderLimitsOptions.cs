using Microsoft.Extensions.Options;

namespace UTTTGO.Api.Modules.Orders.Application;

public sealed class OrderLimitsOptions
{
    public int MaxQuantityPerOrder { get; set; } = 50;

    public int PageSizeMax { get; set; } = 50;

    public int DeliveryTokenTtlSeconds { get; set; } = 900;
}
