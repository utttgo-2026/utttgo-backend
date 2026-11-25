using System.Net;
using System.Text.RegularExpressions;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Orders.Application;

namespace UTTTGO.Api.Modules.Chat.Application;

public sealed class ChatService(IOrderMessageRepository messages, IOrderChatParticipantReader orders,
    IAuthenticatedLocalProfileReader profiles, IChatRateLimitGate rateLimit) : IChatService
{
    public async Task<ChatResult> ListAsync(string token, string orderId, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        try
        {
            var participant = await AuthorizeAsync(token, orderId, cancellationToken);
            if (participant is null) return new(ChatOutcome.Unauthorized);
            var (order, profile) = participant.Value;
            if (!IsParticipant(order, profile)) return new(ChatOutcome.NotFound);
            if (!await rateLimit.IsAllowedAsync(profile.Id, false, cancellationToken)) return new(ChatOutcome.RateLimited);
            var result = await messages.ListAsync(orderId, page, pageSize, cancellationToken);
            return new(ChatOutcome.Success,
                Page: new(result.Items.Select(ToResponse).ToList(), page, pageSize, result.Total));
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new(ChatOutcome.Unavailable);
        }
    }

    public async Task<ChatResult> SendAsync(string token, string orderId, string? content, string? key,
        CancellationToken cancellationToken)
    {
        try
        {
            var participant = await AuthorizeAsync(token, orderId, cancellationToken);
            if (participant is null) return new(ChatOutcome.Unauthorized);
            var (order, profile) = participant.Value;
            if (!IsParticipant(order, profile)) return new(ChatOutcome.NotFound);
            if (OrderStatus.IsTerminal(order.Status)) return new(ChatOutcome.Closed);
            if (string.IsNullOrWhiteSpace(key)) return new(ChatOutcome.InvalidIdempotencyKey);
            if (!await rateLimit.IsAllowedAsync(profile.Id, true, cancellationToken)) return new(ChatOutcome.RateLimited);

            var clean = Sanitize(content);
            if (clean.Length is 0 or > 1000) return new(ChatOutcome.InvalidContent);
            var idempotencyKey = key.Trim();
            if (idempotencyKey.Length > 128) return new(ChatOutcome.InvalidIdempotencyKey);
            var existing = await messages.FindByIdempotencyKeyAsync(orderId, profile.Id, idempotencyKey,
                cancellationToken);
            if (existing is not null)
                return string.Equals(existing.Content, clean, StringComparison.Ordinal)
                    ? new(ChatOutcome.Duplicate, ToResponse(existing))
                    : new(ChatOutcome.IdempotencyKeyReused);

            var message = new OrderMessageRecord(string.Empty, orderId, profile.Id, clean, DateTime.UtcNow,
                idempotencyKey);
            try
            {
                var created = await messages.InsertAsync(message, cancellationToken);
                return new(ChatOutcome.Created, ToResponse(created));
            }
            catch (DuplicateOrderMessageException)
            {
                var replay = await messages.FindByIdempotencyKeyAsync(orderId, profile.Id, idempotencyKey,
                    cancellationToken);
                if (replay is null) return new(ChatOutcome.Unavailable);
                return string.Equals(replay.Content, clean, StringComparison.Ordinal)
                    ? new(ChatOutcome.Duplicate, ToResponse(replay))
                    : new(ChatOutcome.IdempotencyKeyReused);
            }
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new(ChatOutcome.Unavailable);
        }
    }

    private async Task<(OrderChatSnapshot Order, LocalLoginProfile Profile)?> AuthorizeAsync(string token, string orderId,
        CancellationToken cancellationToken)
    {
        var profile = await profiles.ReadAsync(token, cancellationToken);
        if (profile.Outcome != AuthenticatedLocalProfileOutcome.Success || profile.Profile is null ||
            !string.Equals(profile.Profile.AccountStatus, "Active", StringComparison.Ordinal)) return null;
        var order = await orders.FindForChatAsync(orderId, cancellationToken);
        return order is null ? null : (order, profile.Profile);
    }

    private static bool IsParticipant(OrderChatSnapshot order, LocalLoginProfile profile) =>
        order.BuyerUserId == profile.Id ||
        (!string.IsNullOrWhiteSpace(order.SellerId) && !string.IsNullOrWhiteSpace(profile.SellerId) &&
         order.SellerId == profile.SellerId);

    private static string Sanitize(string? content)
    {
        var plain = Regex.Replace(WebUtility.HtmlDecode(content ?? string.Empty), "<[^>]*>", string.Empty);
        plain = Regex.Replace(plain, "[\\u0000-\\u0008\\u000B\\u000C\\u000E-\\u001F]", string.Empty);
        return plain.Trim();
    }

    private static OrderMessageResponse ToResponse(OrderMessageRecord message) =>
        new(message.Id, message.AuthorId, message.Content, message.CreatedAt);
}

public interface IChatService
{
    Task<ChatResult> ListAsync(string token, string orderId, int page, int pageSize, CancellationToken cancellationToken);
    Task<ChatResult> SendAsync(string token, string orderId, string? content, string? key, CancellationToken cancellationToken);
}

public sealed class DuplicateOrderMessageException : Exception;
