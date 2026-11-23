using MongoDB.Bson;
using MongoDB.Driver;
using UTTTGO.Api.Modules.Reviews.Application;
using UTTTGO.Api.Shared.Infrastructure;

namespace UTTTGO.Api.Modules.Reviews.Infrastructure;

public sealed class MongoReviewRepository(MongoStore store) : IReviewRepository
{
    private IMongoCollection<ReviewDocument> Reviews => store.Database.GetCollection<ReviewDocument>("reviews");

    public async Task<ReviewRecord> CreateAsync(ReviewRecord review, CancellationToken cancellationToken)
    {
        var document = new ReviewDocument
        {
            Id = ObjectId.GenerateNewId(),
            OrderId = review.OrderId,
            BuyerUserId = review.BuyerUserId,
            SellerId = review.SellerId,
            ProductId = review.ProductId,
            Rating = review.Rating,
            Comment = review.Comment,
            CreatedAt = review.CreatedAt
        };
        try
        {
            await Reviews.InsertOneAsync(document, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            throw new DuplicateReviewException();
        }
        return ToRecord(document);
    }

    public Task<bool> ExistsForOrderAsync(string orderId, CancellationToken cancellationToken) =>
        Reviews.Find(item => item.OrderId == orderId).Limit(1).AnyAsync(cancellationToken);

    public async Task<ReviewPage> ListForSellerAsync(string sellerId, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        var filter = Builders<ReviewDocument>.Filter.Eq(item => item.SellerId, sellerId);
        var total = await Reviews.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var documents = await Reviews.Find(filter)
            .Sort(Builders<ReviewDocument>.Sort.Descending(item => item.CreatedAt).Ascending(item => item.Id))
            .Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(cancellationToken);
        return new(documents.Select(ToRecord).ToList(), total);
    }

    public async Task<ReviewRatings> GetRatingsForSellerAsync(string sellerId,
        CancellationToken cancellationToken) => await GetRatingsAsync(
            Builders<ReviewDocument>.Filter.Eq(item => item.SellerId, sellerId), cancellationToken);

    public async Task<ReviewRatings> GetRatingsForProductAsync(string productId,
        CancellationToken cancellationToken) => await GetRatingsAsync(
            Builders<ReviewDocument>.Filter.Eq(item => item.ProductId, productId), cancellationToken);

    private async Task<ReviewRatings> GetRatingsAsync(FilterDefinition<ReviewDocument> filter,
        CancellationToken cancellationToken)
    {
        var documents = await Reviews.Find(filter).Project(item => item.Rating).ToListAsync(cancellationToken);
        var breakdown = Enumerable.Range(1, 5).ToDictionary(rating => rating,
            rating => (long)documents.Count(value => value == rating));
        return new(documents.Count == 0 ? null : Math.Round((decimal)documents.Sum() / documents.Count, 2),
            documents.Count, breakdown);
    }

    private static ReviewRecord ToRecord(ReviewDocument review) => new(review.Id.ToString(), review.OrderId,
        review.BuyerUserId, review.SellerId, review.ProductId, review.Rating, review.Comment, review.CreatedAt);
}
