using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Orders.Application;
using UTTTGO.Api.Modules.Reviews.Contracts;

namespace UTTTGO.Api.Modules.Reviews.Application;

public interface ICreateReviewService
{
    Task<CreateReviewResult> CreateAsync(string token, string orderId, CreateReviewRequest request,
        CancellationToken cancellationToken);
}

public enum CreateReviewOutcome
{
    Created, Unauthorized, AccountNotActive, NotFound, AlreadyExists, RateLimited, Unavailable
}

public sealed record CreateReviewResult(CreateReviewOutcome Outcome, ReviewResponse? Response = null);

public sealed class CreateReviewService(IAuthenticatedLocalProfileReader profiles,
    IDeliveredOrderReader orders, IReviewsRateLimitGate rateLimit, IReviewRepository reviews,
    ILogger<CreateReviewService> logger) : ICreateReviewService
{
    public async Task<CreateReviewResult> CreateAsync(string token, string orderId,
        CreateReviewRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var authorization = await profiles.ReadAsync(token, cancellationToken);
            if (authorization.Outcome is AuthenticatedLocalProfileOutcome.Unauthorized
                or AuthenticatedLocalProfileOutcome.ProfileMissing)
                return new(CreateReviewOutcome.Unauthorized);
            if (authorization.Outcome != AuthenticatedLocalProfileOutcome.Success ||
                authorization.Profile is null)
                return new(CreateReviewOutcome.Unavailable);

            var profile = authorization.Profile;
            if (!string.Equals(profile.AccountStatus, "Active", StringComparison.Ordinal))
                return new(CreateReviewOutcome.AccountNotActive);
            if (!await rateLimit.IsAllowedForCreateAsync(profile.Id, cancellationToken))
                return new(CreateReviewOutcome.RateLimited);

            var delivered = await orders.FindDeliveredForBuyerAsync(orderId, profile.Id, cancellationToken);
            if (delivered is null) return new(CreateReviewOutcome.NotFound);
            if (await reviews.ExistsForOrderAsync(orderId, cancellationToken))
                return new(CreateReviewOutcome.AlreadyExists);

            var review = new ReviewRecord(string.Empty, delivered.OrderId, delivered.BuyerUserId,
                delivered.SellerId, delivered.ProductId, request.Rating!.Value,
                string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
                DateTime.UtcNow);
            try
            {
                var created = await reviews.CreateAsync(review, cancellationToken);
                return new(CreateReviewOutcome.Created, ToResponse(created));
            }
            catch (DuplicateReviewException)
            {
                return new(CreateReviewOutcome.AlreadyExists);
            }
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError("Review creation failed for order {OrderId}.", orderId);
            return new(CreateReviewOutcome.Unavailable);
        }
    }

    private static ReviewResponse ToResponse(ReviewRecord review) =>
        new(review.ReviewId, review.SellerId, review.ProductId, review.Rating, review.Comment,
            review.CreatedAt);
}

public sealed class DuplicateReviewException : Exception
{
    public DuplicateReviewException() : base("Duplicate review.") { }
}
