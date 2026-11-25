using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StackExchange.Redis;
using UTTTGO.Api.Modules.Catalog.Application;
using UTTTGO.Api.Modules.Catalog.Contracts;

namespace UTTTGO.Api.Modules.Catalog.Infrastructure;

public sealed class RedisCatalogCache(IServiceProvider services) : ICatalogCache
{
    private static readonly TimeSpan EntryLifetime = TimeSpan.FromSeconds(60);
    private const string VersionKey = "cache:catalog:version";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ProductListResponse?> GetProductsAsync(ProductListQuery query,
        CancellationToken cancellationToken)
    {
        var key = await KeyAsync("products", query, cancellationToken);
        return key is null ? null : await GetAsync<ProductListResponse>(key, cancellationToken);
    }

    public async Task SetProductsAsync(ProductListQuery query, ProductListResponse response,
        CancellationToken cancellationToken)
    {
        var key = await KeyAsync("products", query, cancellationToken);
        if (key is not null) await SetAsync(key, response, cancellationToken);
    }

    public async Task<CategoryListResponse?> GetCategoriesAsync(int page, int pageSize,
        CancellationToken cancellationToken)
    {
        var key = await KeyAsync("categories", new { page, pageSize }, cancellationToken);
        return key is null ? null : await GetAsync<CategoryListResponse>(key, cancellationToken);
    }

    public async Task SetCategoriesAsync(int page, int pageSize, CategoryListResponse response,
        CancellationToken cancellationToken)
    {
        var key = await KeyAsync("categories", new { page, pageSize }, cancellationToken);
        if (key is not null) await SetAsync(key, response, cancellationToken);
    }

    public async Task InvalidateAsync(CancellationToken cancellationToken)
    {
        try
        {
            var connection = Connection;
            if (connection is null) return;
            await connection.GetDatabase().StringIncrementAsync(VersionKey).WaitAsync(cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Cache invalidation is best effort; MongoDB remains the source of truth.
        }
    }

    private async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            var connection = Connection;
            if (connection is null) return null;
            var value = await connection.GetDatabase().StringGetAsync(key).WaitAsync(cancellationToken);
            return value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<T>(value.ToString(), JsonOptions);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private async Task SetAsync<T>(string key, T value, CancellationToken cancellationToken)
    {
        try
        {
            var connection = Connection;
            if (connection is null) return;
            var payload = JsonSerializer.Serialize(value, JsonOptions);
            await connection.GetDatabase().StringSetAsync(key, payload, EntryLifetime)
                .WaitAsync(cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // A Redis outage must not turn a successful catalog read into a failure.
        }
    }

    private async Task<string?> KeyAsync(string resource, object request, CancellationToken cancellationToken)
    {
        try
        {
            var connection = Connection;
            if (connection is null) return null;
            var version = await connection.GetDatabase().StringGetAsync(VersionKey).WaitAsync(cancellationToken);
            var versionText = version.IsNullOrEmpty ? "0" : version.ToString();
            var canonical = JsonSerializer.Serialize(request, JsonOptions);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
            return $"cache:catalog:{resource}:v{versionText}:{hash}";
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private IConnectionMultiplexer? Connection
    {
        get
        {
            try
            {
                return services.GetRequiredService<IConnectionMultiplexer>();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
