namespace UTTTGO.Api.Modules.Chat.Application;

public sealed record SendOrderMessageRequest(string? Content);
public sealed record OrderMessageResponse(string Id, string AuthorId, string Content, DateTime CreatedAt);
public sealed record OrderMessagesResponse(IReadOnlyList<OrderMessageResponse> Items, int Page, int PageSize,
    long TotalCount);

public interface IChatRateLimitGate
{
    Task<bool> IsAllowedAsync(string userId, bool write, CancellationToken cancellationToken);
}

public interface IOrderMessageRepository
{
    Task<OrderMessageRecord?> FindByIdempotencyKeyAsync(string orderId, string authorId, string key,
        CancellationToken cancellationToken);
    Task<OrderMessageRecord> InsertAsync(OrderMessageRecord message, CancellationToken cancellationToken);
    Task<(IReadOnlyList<OrderMessageRecord> Items, long Total)> ListAsync(string orderId, int page, int pageSize,
        CancellationToken cancellationToken);
}

public sealed record OrderMessageRecord(string Id, string OrderId, string AuthorId, string Content,
    DateTime CreatedAt, string IdempotencyKey);

public enum ChatOutcome
{
    Success, Created, Duplicate, IdempotencyKeyReused, InvalidContent, InvalidIdempotencyKey, Unauthorized, NotFound, Closed,
    RateLimited, Unavailable
}
public sealed record ChatResult(ChatOutcome Outcome, OrderMessageResponse? Message = null,
    OrderMessagesResponse? Page = null);
