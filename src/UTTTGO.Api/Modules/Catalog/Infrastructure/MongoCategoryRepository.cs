using MongoDB.Bson;
using MongoDB.Driver;
using UTTTGO.Api.Modules.Catalog.Application;
using UTTTGO.Api.Shared.Infrastructure;

namespace UTTTGO.Api.Modules.Catalog.Infrastructure;

public sealed class MongoCategoryRepository(MongoStore store) : ICategoryRepository
{
    private IMongoCollection<CategoryDocument> Categories =>
        store.Database.GetCollection<CategoryDocument>("categories");

    public async Task<CategoryRecord?> FindByIdAsync(string categoryId, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(categoryId, out var id)) return null;
        var document = await Categories.Find(category => category.Id == id)
            .FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : ToRecord(document);
    }

    public async Task<IReadOnlyList<CategoryRecord>> ListActiveAsync(CancellationToken cancellationToken)
    {
        var documents = await Categories.Find(category => category.IsActive)
            .Sort(Builders<CategoryDocument>.Sort.Ascending(category => category.SortOrder)
                .Ascending(category => category.Name))
            .ToListAsync(cancellationToken);
        return documents.Select(ToRecord).ToList();
    }

    public async Task<CategoryPage> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var filter = Builders<CategoryDocument>.Filter.Eq(category => category.IsActive, true);
        var total = await Categories.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var skip = (int)((long)(page - 1) * pageSize);
        var documents = await Categories.Find(filter)
            .Sort(Builders<CategoryDocument>.Sort.Ascending(category => category.SortOrder)
                .Ascending(category => category.Name))
            .Skip(skip).Limit(pageSize).ToListAsync(cancellationToken);
        return new(documents.Select(ToRecord).ToList(), total);
    }

    private static CategoryRecord ToRecord(CategoryDocument category) =>
        new(category.Id.ToString(), category.Name, category.IsActive, category.SortOrder);
}
