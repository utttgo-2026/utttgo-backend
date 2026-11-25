using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using MongoDB.Driver;
using UTTTGO.Api.Modules.Notifications.Application;
using UTTTGO.Api.Shared.Infrastructure;

namespace UTTTGO.Api.Modules.Notifications.Infrastructure;

public sealed class MongoDeviceTokenStore(MongoStore store, IDataProtectionProvider protection) : IDeviceTokenStore
{
    private IMongoCollection<DeviceTokenDocument> Collection => store.Database.GetCollection<DeviceTokenDocument>("deviceTokens");
    private readonly IDataProtector protector = protection.CreateProtector("utttgo.notifications.device-token.v1");
    public async Task<DeviceTokenRegistration> UpsertAsync(string userId, string token, string installationId,
        string platform, string? appVersion, CancellationToken ct)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        var now = DateTime.UtcNow;
        await Collection.UpdateManyAsync(x => x.TokenHash == hash && x.Active,
            Builders<DeviceTokenDocument>.Update.Set(x => x.Active, false)
                .Set(x => x.UpdatedAt, now), cancellationToken: ct);
        var filter = Builders<DeviceTokenDocument>.Filter.Eq(x => x.UserId, userId) &
            Builders<DeviceTokenDocument>.Filter.Eq(x => x.InstallationId, installationId);
        var existing = await Collection.Find(filter).FirstOrDefaultAsync(ct);
        var id = existing?.Id ?? MongoDB.Bson.ObjectId.GenerateNewId();
        var update = Builders<DeviceTokenDocument>.Update.Set(x => x.UserId, userId)
            .Set(x => x.ProtectedToken, protector.Protect(token)).Set(x => x.TokenHash, hash)
            .Set(x => x.Platform, platform).Set(x => x.AppVersion, appVersion).Set(x => x.Active, true)
            .Set(x => x.LastSeenAt, now).Set(x => x.UpdatedAt, now).SetOnInsert(x => x.CreatedAt, now)
            .SetOnInsert(x => x.Id, id);
        await Collection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, ct);
        return new(id.ToString(), installationId, platform, existing is null, now);
    }
    public async Task<bool> RemoveAsync(string userId, string id, CancellationToken ct)
    {
        if (!MongoDB.Bson.ObjectId.TryParse(id, out var objectId)) return false;
        return (await Collection.DeleteOneAsync(x => x.UserId == userId && x.Id == objectId, ct)).DeletedCount > 0;
    }
    public Task RemoveForUserAsync(string userId, CancellationToken ct) => Collection.DeleteManyAsync(x => x.UserId == userId, ct);
    public async Task<IReadOnlyList<DeviceTokenRecord>> FindActiveAsync(string userId, CancellationToken ct) =>
        (await Collection.Find(x => x.UserId == userId && x.Active).ToListAsync(ct))
        .Select(x => new DeviceTokenRecord(x.Id.ToString(), x.UserId, x.ProtectedToken, x.TokenHash)).ToList();
    public Task DeactivateAsync(string id, CancellationToken ct)
    {
        if (!MongoDB.Bson.ObjectId.TryParse(id, out var objectId)) return Task.CompletedTask;
        return Collection.UpdateOneAsync(x => x.Id == objectId, Builders<DeviceTokenDocument>.Update.Set(x => x.Active, false).Set(x => x.UpdatedAt, DateTime.UtcNow), cancellationToken: ct);
    }
    public string Unprotect(string value) => protector.Unprotect(value);
}
