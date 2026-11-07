using Microsoft.Extensions.Options;
using MongoDB.Driver;
using UTTTGO.Api.Modules.Identity.Application;

namespace UTTTGO.Api.Shared.Infrastructure;

public sealed class MongoOptions
{
    public string? Uri { get; set; }
    public string Database { get; set; } = "utttgo";
}

// Resolve the client when an adapter runs, not while constructing a controller. A missing URI
// must be caught by the owning service and returned as 503, never as a developer error page.
public sealed class MongoStore(IServiceProvider services, IOptions<MongoOptions> options)
{
    public IMongoDatabase Database
    {
        get
        {
            var name = options.Value.Database;
            if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(['/', '\\', '.', ' ', '"', '$']) >= 0)
                throw new ExternalDependencyUnavailableException();
            return services.GetRequiredService<IMongoClient>().GetDatabase(name);
        }
    }
}
