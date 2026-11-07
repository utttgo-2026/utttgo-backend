using Microsoft.Extensions.Options;
using MongoDB.Driver;
using UTTTGO.Api.Shared.Infrastructure;

namespace UTTTGO.Api.Modules.Identity.Infrastructure;

public sealed class IdentityMongoIndexes(MongoStore store, IOptions<MongoOptions> options,
    ILogger<IdentityMongoIndexes> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.Uri)) return;
        try
        {
            var users = store.Database.GetCollection<UserDocument>("users");
            var keys = Builders<UserDocument>.IndexKeys;
            await users.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<UserDocument>(keys.Ascending(user => user.SupabaseUserId),
                    new CreateIndexOptions { Name = "supabaseUserId_unique", Unique = true }),
                new CreateIndexModel<UserDocument>(keys.Descending(user => user.CreatedAt).Ascending(user => user.Id),
                    new CreateIndexOptions { Name = "createdAt_desc_id_asc" }),
                new CreateIndexModel<UserDocument>(keys.Ascending(user => user.CreatedAt).Ascending(user => user.Id),
                    new CreateIndexOptions { Name = "createdAt_asc_id_asc" }),
                new CreateIndexModel<UserDocument>(keys.Ascending(user => user.AccountStatus)
                    .Descending(user => user.CreatedAt), new CreateIndexOptions { Name = "status_createdAt_desc" }),
                new CreateIndexModel<UserDocument>(keys.Ascending(user => user.Roles)
                    .Descending(user => user.CreatedAt), new CreateIndexOptions { Name = "roles_createdAt_desc" })
            ], cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("MongoDB index creation failed; API startup will continue.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
