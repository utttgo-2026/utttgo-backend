using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using UTTTGO.Api.Modules.Catalog.Application;
using UTTTGO.Api.Modules.Catalog.Contracts;
using UTTTGO.Api.Shared.Infrastructure;

namespace UTTTGO.Api.Modules.Catalog.Infrastructure;

public sealed class MongoProductRepository(MongoStore store) : IProductRepository
{
    private IMongoCollection<ProductDocument> Products =>
        store.Database.GetCollection<ProductDocument>("products");

    public async Task<ProductPage> ListAsync(ProductListQuery query, CancellationToken cancellationToken)
    {
        var filter = Builders<ProductDocument>.Filter.Eq(product => product.Status,
            ProductAvailability.Active);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // The search term is normalized and escaped: no regular expression is taken from the client.
            var term = Regex.Escape(TextNormalizer.Normalize(query.Search));
            filter &= Builders<ProductDocument>.Filter.Regex(product => product.SearchText,
                new BsonRegularExpression(term, "i"));
        }

        if (!string.IsNullOrWhiteSpace(query.CategoryId))
            filter &= Builders<ProductDocument>.Filter.Eq(product => product.CategoryId, query.CategoryId);
        if (query.AllowedCategoryIds is { } allowed)
            filter &= Builders<ProductDocument>.Filter.In(product => product.CategoryId, allowed);
        if (query.MinPrice is { } minPrice)
            filter &= Builders<ProductDocument>.Filter.Gte(product => product.Price, minPrice);
        if (query.MaxPrice is { } maxPrice)
            filter &= Builders<ProductDocument>.Filter.Lte(product => product.Price, maxPrice);

        var total = await Products.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var sort = Builders<ProductDocument>.Sort;
        var ordering = query.SortBy switch
        {
            ProductSort.PriceAsc => sort.Ascending(product => product.Price).Ascending(product => product.Id),
            ProductSort.PriceDesc => sort.Descending(product => product.Price).Ascending(product => product.Id),
            ProductSort.NameAsc => sort.Ascending(product => product.NormalizedName).Ascending(product => product.Id),
            _ => sort.Descending(product => product.CreatedAt).Ascending(product => product.Id)
        };

        var skip = (int)((long)(query.Page - 1) * query.PageSize);
        var documents = await Products.Find(filter).Sort(ordering).Skip(skip).Limit(query.PageSize)
            .ToListAsync(cancellationToken);
        return new(documents.Select(ToRecord).ToList(), total);
    }

    public async Task<ProductRecord?> FindByIdAsync(string productId, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(productId, out var id)) return null;
        var document = await Products.Find(product => product.Id == id).FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : ToRecord(document);
    }

    public async Task<ProductRecord> CreateAsync(NewProduct product, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var document = new ProductDocument
        {
            Id = ObjectId.GenerateNewId(),
            SellerId = product.SellerId,
            Name = product.Name,
            NormalizedName = TextNormalizer.Normalize(product.Name),
            Description = product.Description,
            Ingredients = product.Ingredients,
            CategoryId = product.CategoryId,
            Price = product.Price,
            Stock = product.Stock,
            Status = ProductAvailability.Active,
            ImageAssetId = product.ImageAssetId,
            ImageObjectKey = product.ImageObjectKey,
            SearchText = BuildSearchText(product.Name, product.Description, product.Ingredients),
            Version = 0,
            CreatedAt = now,
            UpdatedAt = now
        };

        await Products.InsertOneAsync(document, cancellationToken: cancellationToken);
        return ToRecord(document);
    }

    public async Task<ProductUpdateResult> UpdateOwnAsync(string sellerId, string productId,
        long expectedVersion, ProductChanges changes, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(productId, out var id)) return new(ProductUpdateOutcome.Missing);
        var filter = Builders<ProductDocument>.Filter.Eq(product => product.Id, id) &
            Builders<ProductDocument>.Filter.Eq(product => product.SellerId, sellerId) &
            Builders<ProductDocument>.Filter.Eq(product => product.Version, expectedVersion) &
            Builders<ProductDocument>.Filter.Ne(product => product.Status, ProductAvailability.Deleted);

        var updates = new List<UpdateDefinition<ProductDocument>>
        {
            Builders<ProductDocument>.Update.Inc(product => product.Version, 1),
            Builders<ProductDocument>.Update.Set(product => product.UpdatedAt, DateTime.UtcNow)
        };
        if (changes.HasName)
            updates.Add(Builders<ProductDocument>.Update.Set(product => product.Name, changes.Name!));
        if (changes.HasDescription)
            updates.Add(Builders<ProductDocument>.Update.Set(product => product.Description, changes.Description!));
        if (changes.HasIngredients)
            updates.Add(Builders<ProductDocument>.Update.Set(product => product.Ingredients,
                changes.Ingredients!));
        if (changes.HasPrice)
            updates.Add(Builders<ProductDocument>.Update.Set(product => product.Price, changes.Price!.Value));
        if (changes.HasStock)
            updates.Add(Builders<ProductDocument>.Update.Set(product => product.Stock, changes.Stock!.Value));
        if (changes.HasImageUrl)
        {
            updates.Add(Builders<ProductDocument>.Update.Set(product => product.ImageAssetId,
                changes.ImageAssetId));
            updates.Add(Builders<ProductDocument>.Update.Set(product => product.ImageObjectKey,
                changes.ImageObjectKey));
        }

        var updated = await Products.FindOneAndUpdateAsync(filter,
            Builders<ProductDocument>.Update.Combine(updates),
            new FindOneAndUpdateOptions<ProductDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
        if (updated is not null) return new(ProductUpdateOutcome.Updated, ToRecord(updated));

        var exists = await Products.Find(product => product.Id == id && product.SellerId == sellerId)
            .AnyAsync(cancellationToken);
        return new(exists ? ProductUpdateOutcome.Conflict : ProductUpdateOutcome.Missing);
    }

    public async Task<ProductUpdateResult> UpdateAvailabilityAsync(string sellerId, string productId,
        long expectedVersion, string status, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(productId, out var id)) return new(ProductUpdateOutcome.Missing);
        var filter = Builders<ProductDocument>.Filter.Eq(product => product.Id, id) &
            Builders<ProductDocument>.Filter.Eq(product => product.SellerId, sellerId) &
            Builders<ProductDocument>.Filter.Eq(product => product.Version, expectedVersion) &
            Builders<ProductDocument>.Filter.Ne(product => product.Status, ProductAvailability.Deleted);

        var updated = await Products.FindOneAndUpdateAsync(filter,
            Builders<ProductDocument>.Update.Combine(
                Builders<ProductDocument>.Update.Set(product => product.Status, status),
                Builders<ProductDocument>.Update.Inc(product => product.Version, 1),
                Builders<ProductDocument>.Update.Set(product => product.UpdatedAt, DateTime.UtcNow)),
            new FindOneAndUpdateOptions<ProductDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
        if (updated is not null) return new(ProductUpdateOutcome.Updated, ToRecord(updated));

        var exists = await Products.Find(product => product.Id == id && product.SellerId == sellerId)
            .AnyAsync(cancellationToken);
        return new(exists ? ProductUpdateOutcome.Conflict : ProductUpdateOutcome.Missing);
    }

    internal static string BuildSearchText(string name, string description,
        IReadOnlyList<string> ingredients)
    {
        var parts = new List<string>
        {
            TextNormalizer.Normalize(name),
            TextNormalizer.Normalize(description)
        };
        parts.AddRange(ingredients.Select(TextNormalizer.Normalize));
        return string.Join(' ', parts.Where(part => part.Length > 0));
    }

    private static ProductRecord ToRecord(ProductDocument product) =>
        new(product.Id.ToString(), product.SellerId, product.Name, product.Description,
            product.Ingredients, product.CategoryId, product.Price, product.Stock, product.Status,
            product.Version, product.CreatedAt, product.UpdatedAt, product.ImageAssetId,
            product.ImageObjectKey);
}
