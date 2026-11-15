using MongoDB.Bson;
using MongoDB.Driver;
using UTTTGO.Api.Shared.Infrastructure;

namespace UTTTGO.Api.Modules.Catalog.Infrastructure;

public sealed class MongoCatalogSeeder(MongoStore store) : ICatalogSeeder
{
    public async Task<IReadOnlyList<string>> SeedAsync(CancellationToken cancellationToken)
    {
        var categories = store.Database.GetCollection<CategoryDocument>("categories");
        var seeded = new List<string>();
        foreach (var seed in CatalogSeedData.Categories)
        {
            var normalizedName = CatalogSeedData.NormalizedName(seed.Name);
            var filter = Builders<CategoryDocument>.Filter.Eq(item => item.NormalizedName, normalizedName);
            var update = Builders<CategoryDocument>.Update
                .SetOnInsert(item => item.Id, ObjectId.GenerateNewId())
                .SetOnInsert(item => item.Name, seed.Name)
                .SetOnInsert(item => item.NormalizedName, normalizedName)
                .SetOnInsert(item => item.CreatedAt, DateTime.UtcNow)
                .Set(item => item.IsActive, true)
                .Set(item => item.SortOrder, seed.SortOrder)
                .Set(item => item.UpdatedAt, DateTime.UtcNow);
            var document = await categories.FindOneAndUpdateAsync(filter, update,
                new FindOneAndUpdateOptions<CategoryDocument>
                {
                    IsUpsert = true,
                    ReturnDocument = ReturnDocument.After
                }, cancellationToken);
            seeded.Add(document?.Id.ToString() ?? string.Empty);
        }

        return seeded;
    }
}
