using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Sellers.Application;

namespace UtttGo.SecurityTests;

internal sealed class InMemorySellerWriter : ISellerProfileWriter
{
    public Dictionary<string, SellerProfileRecord> Sellers { get; } = new(StringComparer.Ordinal);
    public int CreateCalls { get; private set; }
    public int UpdateCalls { get; private set; }
    public string? LastOwnerUserId { get; private set; }
    public bool Fail { get; set; }

    public Task<SellerProfileRecord?> FindByOwnerAsync(string ownerUserId, CancellationToken cancellationToken)
    {
        if (Fail) throw new InvalidOperationException("internal-secret");
        LastOwnerUserId = ownerUserId;
        Sellers.TryGetValue(ownerUserId, out var seller);
        return Task.FromResult(seller);
    }

    public Task<SellerProfileRecord> CreatePendingAsync(string ownerUserId, string businessName,
        string description, string pickupPoint, CancellationToken cancellationToken)
    {
        if (Fail) throw new InvalidOperationException("internal-secret");
        CreateCalls++;
        LastOwnerUserId = ownerUserId;
        var record = new SellerProfileRecord("507f1f77bcf86cd799439011", ownerUserId, businessName, description,
            pickupPoint, "PendingActivation", 0, DateTime.UtcNow, DateTime.UtcNow);
        Sellers[ownerUserId] = record;
        return Task.FromResult(record);
    }

    public Task<SellerProfileRecord?> ActivateAsync(string sellerId, CancellationToken cancellationToken)
    {
        if (Fail) throw new InvalidOperationException("internal-secret");
        var entry = Sellers.FirstOrDefault(item => item.Value.SellerId == sellerId);
        if (entry.Key is null || entry.Value.Status != "PendingActivation")
            return Task.FromResult<SellerProfileRecord?>(null);
        var activated = entry.Value with
        {
            Status = "Active",
            Version = entry.Value.Version + 1,
            UpdatedAt = DateTime.UtcNow
        };
        Sellers[entry.Key] = activated;
        return Task.FromResult<SellerProfileRecord?>(activated);
    }

    public Task<SellerProfileUpdateResult> UpdateOwnAsync(string ownerUserId, string sellerId,
        long expectedVersion, string? businessName, bool changeBusinessName, string? description,
        bool changeDescription, string? pickupPoint, bool changePickupPoint, CancellationToken cancellationToken)
    {
        if (Fail) throw new InvalidOperationException("internal-secret");
        UpdateCalls++;
        LastOwnerUserId = ownerUserId;
        if (!Sellers.TryGetValue(ownerUserId, out var seller) || seller.SellerId != sellerId)
            return Task.FromResult(new SellerProfileUpdateResult(SellerProfileUpdateOutcome.Missing));
        if (seller.Version != expectedVersion)
            return Task.FromResult(new SellerProfileUpdateResult(SellerProfileUpdateOutcome.Conflict));

        var updated = seller with
        {
            BusinessName = changeBusinessName ? businessName! : seller.BusinessName,
            Description = changeDescription ? description! : seller.Description,
            PickupPoint = changePickupPoint ? pickupPoint! : seller.PickupPoint,
            Version = seller.Version + 1,
            UpdatedAt = DateTime.UtcNow
        };
        Sellers[ownerUserId] = updated;
        return Task.FromResult(new SellerProfileUpdateResult(SellerProfileUpdateOutcome.Updated, updated));
    }
}

internal sealed class PublicSellerProfileDouble(SellerProfileSnapshot? seller) : ISellerProfileRepository
{
    public Task<SellerProfileSnapshot?> FindByIdAsync(string sellerId, CancellationToken cancellationToken) =>
        Task.FromResult(seller);
}

internal sealed class SellerRolePromotionDouble : ISellerRolePromotion
{
    public int Calls { get; private set; }
    public string? LastUserId { get; private set; }
    public string? LastSellerId { get; private set; }

    public Task<SellerRolePromotionResult> PromoteAsync(string subject, string localUserId, string sellerId,
        CancellationToken cancellationToken)
    {
        Calls++;
        LastUserId = localUserId;
        LastSellerId = sellerId;
        return Task.FromResult(new SellerRolePromotionResult(SellerRolePromotionOutcome.Promoted,
            ["Buyer", "Seller"]));
    }
}

internal sealed class SellerRateLimitDouble : ISellerRateLimitGate
{
    public bool Allowed { get; set; } = true;
    public bool Fail { get; set; }

    public Task<bool> IsAllowedForActivationAsync(string userId, CancellationToken cancellationToken) => Check();

    public Task<bool> IsAllowedForWriteAsync(string userId, CancellationToken cancellationToken) => Check();

    public Task<bool> IsAllowedForReadAsync(string userId, CancellationToken cancellationToken) => Check();

    private Task<bool> Check() =>
        Fail ? throw new InvalidOperationException("internal-secret") : Task.FromResult(Allowed);
}

internal sealed class AuthenticatedProfileDouble : IAuthenticatedLocalProfileReader
{
    public AuthenticatedLocalProfileOutcome Outcome { get; set; } = AuthenticatedLocalProfileOutcome.Success;
    public LocalLoginProfile? Profile { get; set; }

    public Task<AuthenticatedLocalProfileResult> ReadAsync(string token, CancellationToken cancellationToken) =>
        Task.FromResult(new AuthenticatedLocalProfileResult(Outcome, Profile));
}

internal sealed class AllowedPublicGate : IPublicSellerRateLimitGate
{
    public Task<bool> IsAllowedAsync(string sellerId, CancellationToken cancellationToken) => Task.FromResult(true);
}

internal sealed class NoRatings : ISellerRatingsReader
{
    public Task<SellerRatingsSnapshot?> GetForSellerAsync(string sellerId, CancellationToken cancellationToken) =>
        Task.FromResult<SellerRatingsSnapshot?>(null);
}

internal static class SellerFixtures
{
    public const string UserId = "local-id";
    public const string Subject = "sub-1";
    public const string SellerId = "507f1f77bcf86cd799439011";

    public static LocalLoginProfile Profile(string status = "Active", bool withSellerRole = false,
        string? sellerId = null) =>
        new(UserId, Subject, "a@uttt.edu.mx", status, withSellerRole ? ["Buyer", "Seller"] : ["Buyer"],
            "Alumna", "1234567890", null, 3, sellerId);

    public static SellerProfileRecord Seller(string ownerUserId = UserId, string sellerId = SellerId,
        string status = "Active", long version = 1) =>
        new(sellerId, ownerUserId, "Campus Snacks", "Snacks del campus", "Biblioteca central", status, version,
            new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc));
}
