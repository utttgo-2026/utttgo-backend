using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using UTTTGO.Api.Modules.Catalog.Infrastructure;
using UTTTGO.Api.Modules.Identity.Infrastructure;
using UTTTGO.Api.Modules.Orders.Infrastructure;
using UTTTGO.Api.Modules.Sellers.Infrastructure;

namespace UtttGo.SecurityTests;

public sealed class WebApplicationFactory<T> : Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<T>
    where T : class
{
    private readonly Action<IWebHostBuilder>? configure;

    public WebApplicationFactory() { }

    private WebApplicationFactory(Action<IWebHostBuilder> configure) => this.configure = configure;

    public new WebApplicationFactory<T> WithWebHostBuilder(Action<IWebHostBuilder> configuration) =>
        new(builder =>
        {
            configure?.Invoke(builder);
            configuration(builder);
        });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        configure?.Invoke(builder);
        builder.ConfigureTestServices(services =>
        {
            foreach (var entry in services.Where(descriptor =>
                descriptor.ImplementationType == typeof(IdentityMongoIndexes) ||
                descriptor.ImplementationType == typeof(SellersMongoIndexes) ||
                descriptor.ImplementationType == typeof(CatalogMongoIndexes) ||
                descriptor.ImplementationType == typeof(CatalogSeedHostedService) ||
                descriptor.ImplementationType == typeof(OrdersMongoIndexes)).ToArray())
                services.Remove(entry);
        });
    }
}
