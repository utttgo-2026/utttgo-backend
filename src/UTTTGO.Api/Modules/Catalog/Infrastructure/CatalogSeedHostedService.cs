using Microsoft.Extensions.Options;

namespace UTTTGO.Api.Modules.Catalog.Infrastructure;

public sealed class CatalogSeedOptions
{
    public bool Enabled { get; set; }
}

public sealed class CatalogSeedHostedService(ICatalogSeeder seeder, IOptions<CatalogSeedOptions> options,
    ILogger<CatalogSeedHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled) return;
        try
        {
            var seeded = await seeder.SeedAsync(cancellationToken);
            logger.LogInformation("Catalog seed completed with {CategoryCount} categories.", seeded.Count);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Catalog seeding failed; API startup will continue.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
