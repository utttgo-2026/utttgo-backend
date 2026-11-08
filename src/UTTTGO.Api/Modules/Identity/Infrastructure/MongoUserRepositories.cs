using MongoDB.Bson;
using MongoDB.Driver;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Shared.Infrastructure;

namespace UTTTGO.Api.Modules.Identity.Infrastructure;

public sealed class MongoUserRepositories(MongoStore store) : IUserRegistrationRepository,
    ILoginProfileRepository, IProfileUpdateRepository
{
    private IMongoCollection<UserDocument> Users => store.Database.GetCollection<UserDocument>("users");

    public async Task EnsureAvailableAsync(CancellationToken cancellationToken) =>
        await store.Database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1),
            cancellationToken: cancellationToken);

    public async Task SavePendingBuyerAsync(PendingBuyerProfile profile, CancellationToken cancellationToken)
    {
        // The unique supabaseUserId index makes simultaneous registrations idempotent.
        var filter = Builders<UserDocument>.Filter.Eq(user => user.SupabaseUserId, profile.SupabaseUserId) &
            Builders<UserDocument>.Filter.Eq(user => user.Email, profile.Email);
        var update = Builders<UserDocument>.Update
            .SetOnInsert(user => user.Id, ObjectId.GenerateNewId())
            .SetOnInsert(user => user.SupabaseUserId, profile.SupabaseUserId)
            .SetOnInsert(user => user.Email, profile.Email)
            .SetOnInsert(user => user.DisplayName, profile.DisplayName)
            .SetOnInsert(user => user.Roles, [profile.Role])
            .SetOnInsert(user => user.AccountStatus, profile.Status)
            .SetOnInsert(user => user.Version, 0)
            .SetOnInsert(user => user.CreatedAt, DateTime.UtcNow);
        await Users.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, cancellationToken);
    }

    public async Task<LocalLoginProfile?> FindBySubjectAsync(string subject, CancellationToken cancellationToken)
    {
        var user = await Users.Find(item => item.SupabaseUserId == subject)
            .FirstOrDefaultAsync(cancellationToken);
        return user is null ? null : ToProfile(user);
    }

    public async Task<LocalLoginProfile?> ActivatePendingAsync(LocalLoginProfile profile,
        CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(profile.Id, out var id)) return null;
        var filter = Builders<UserDocument>.Filter.Eq(user => user.Id, id) &
            Builders<UserDocument>.Filter.Eq(user => user.SupabaseUserId, profile.SupabaseUserId) &
            Builders<UserDocument>.Filter.Eq(user => user.Email, profile.Email) &
            Builders<UserDocument>.Filter.Eq(user => user.AccountStatus, "PendingEmailVerification") &
            Builders<UserDocument>.Filter.Eq(user => user.Version, profile.Version);
        var update = Builders<UserDocument>.Update.Set(user => user.AccountStatus, "Active")
            .Inc(user => user.Version, 1);
        var updated = await Users.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<UserDocument> { ReturnDocument = ReturnDocument.After }, cancellationToken);
        if (updated is not null) return ToProfile(updated);

        // A concurrent login may already have activated the same profile; never revive another state.
        var current = await FindBySubjectAsync(profile.SupabaseUserId, cancellationToken);
        return current is not null && current.Id == profile.Id && current.Email == profile.Email &&
            current.AccountStatus == "Active" ? current : null;
    }

    public async Task<ProfileUpdateResult> UpdateOwnAsync(string subject, string localId, long expectedVersion,
        string? displayName, bool changeDisplayName, string? phone, bool changePhone,
        CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(localId, out var id)) return new(ProfileUpdateOutcome.Missing);
        var filter = Builders<UserDocument>.Filter.Eq(user => user.Id, id) &
            Builders<UserDocument>.Filter.Eq(user => user.SupabaseUserId, subject) &
            Builders<UserDocument>.Filter.Eq(user => user.Version, expectedVersion) &
            Builders<UserDocument>.Filter.Eq(user => user.AccountStatus, "Active");

        var changes = new List<UpdateDefinition<UserDocument>>
        {
            Builders<UserDocument>.Update.Inc(user => user.Version, 1)
        };
        if (changeDisplayName) changes.Add(Builders<UserDocument>.Update.Set(user => user.DisplayName, displayName!));
        if (changePhone) changes.Add(Builders<UserDocument>.Update.Set(user => user.Phone, phone));
        var updated = await Users.FindOneAndUpdateAsync(filter, Builders<UserDocument>.Update.Combine(changes),
            new FindOneAndUpdateOptions<UserDocument> { ReturnDocument = ReturnDocument.After }, cancellationToken);
        if (updated is not null) return new(ProfileUpdateOutcome.Updated, ToProfile(updated));

        var exists = await Users.Find(user => user.Id == id && user.SupabaseUserId == subject &&
            user.AccountStatus == "Active").AnyAsync(cancellationToken);
        return new(exists ? ProfileUpdateOutcome.Conflict : ProfileUpdateOutcome.Missing);
    }

    private static LocalLoginProfile ToProfile(UserDocument user) =>
        new(user.Id.ToString(), user.SupabaseUserId, user.Email, user.AccountStatus,
            user.Roles, user.DisplayName, user.Phone, user.AvatarMediaAssetId, user.Version, user.SellerId);
}
