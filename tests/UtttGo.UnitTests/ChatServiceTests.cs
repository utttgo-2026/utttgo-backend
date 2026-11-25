using FluentAssertions;
using UTTTGO.Api.Modules.Chat.Application;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Orders.Application;
using Xunit;

namespace UtttGo.UnitTests;

public sealed class ChatServiceTests
{
    [Fact]
    public async Task Send_trims_and_sanitizes_content()
    {
        var messages = new ChatMessages();
        var service = Create(messages);
        var result = await service.SendAsync("token", "order", " <b> hola </b> ", "key", CancellationToken.None);
        result.Outcome.Should().Be(ChatOutcome.Created);
        result.Message!.Content.Should().Be("hola");
    }

    [Fact]
    public async Task Reusing_key_with_different_content_is_rejected()
    {
        var messages = new ChatMessages();
        var service = Create(messages);
        await service.SendAsync("token", "order", "one", "key", CancellationToken.None);
        var result = await service.SendAsync("token", "order", "two", "key", CancellationToken.None);
        result.Outcome.Should().Be(ChatOutcome.IdempotencyKeyReused);
        messages.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Terminal_order_rejects_send()
    {
        var service = Create(new ChatMessages(), "Delivered");
        (await service.SendAsync("token", "order", "hello", "key", CancellationToken.None)).Outcome
            .Should().Be(ChatOutcome.Closed);
    }

    [Fact]
    public async Task Inactive_profile_cannot_access_chat()
    {
        var service = Create(new ChatMessages(), profileStatus: "Suspended");

        (await service.SendAsync("token", "order", "hello", "key", CancellationToken.None)).Outcome
            .Should().Be(ChatOutcome.Unauthorized);
    }

    [Fact]
    public async Task Seller_without_identity_does_not_match_order_without_seller()
    {
        var service = Create(new ChatMessages(), sellerId: null, profileId: "different-user");

        (await service.SendAsync("token", "order", "hello", "key", CancellationToken.None)).Outcome
            .Should().Be(ChatOutcome.NotFound);
    }

    private static ChatService Create(ChatMessages messages, string status = "Pending",
        string profileStatus = "Active", string? sellerId = "seller", string profileId = "user") =>
        new(messages, new ChatOrderReader(status, sellerId),
            new ChatProfileReader(profileStatus, sellerId, profileId), new AllowedChatRateLimit());

    private sealed class ChatMessages : IOrderMessageRepository
    {
        public List<OrderMessageRecord> Items { get; } = [];
        public Task<OrderMessageRecord?> FindByIdempotencyKeyAsync(string orderId, string authorId, string key, CancellationToken ct) =>
            Task.FromResult(Items.SingleOrDefault(x => x.OrderId == orderId && x.AuthorId == authorId && x.IdempotencyKey == key));
        public Task<OrderMessageRecord> InsertAsync(OrderMessageRecord message, CancellationToken ct)
        {
            var item = message with { Id = (Items.Count + 1).ToString() };
            Items.Add(item); return Task.FromResult(item);
        }
        public Task<(IReadOnlyList<OrderMessageRecord> Items, long Total)> ListAsync(string orderId, int page, int pageSize, CancellationToken ct) =>
            Task.FromResult(((IReadOnlyList<OrderMessageRecord>)Items.Where(x => x.OrderId == orderId).ToList(), (long)Items.Count));
    }
    private sealed class ChatOrderReader(string status, string? sellerId) : IOrderChatParticipantReader
    { public Task<OrderChatSnapshot?> FindForChatAsync(string id, CancellationToken ct) => Task.FromResult<OrderChatSnapshot?>(new(id, "user", sellerId!, status)); }
    private sealed class ChatProfileReader(string status, string? sellerId, string profileId) : IAuthenticatedLocalProfileReader
    { public Task<AuthenticatedLocalProfileResult> ReadAsync(string token, CancellationToken ct) => Task.FromResult(new AuthenticatedLocalProfileResult(AuthenticatedLocalProfileOutcome.Success, new LocalLoginProfile(profileId, "sub", "u@uttt.edu.mx", status, ["Buyer"], SellerId: sellerId))); }
    private sealed class AllowedChatRateLimit : IChatRateLimitGate
    { public Task<bool> IsAllowedAsync(string userId, bool write, CancellationToken ct) => Task.FromResult(true); }
}
