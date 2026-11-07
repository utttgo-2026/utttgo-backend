using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Shared.Infrastructure;

namespace UTTTGO.Api.Modules.Identity.Infrastructure;

public sealed class MongoAdminUserListReader(MongoStore store) : IAdminUserListReader
{
    public async Task<AdminUserPage> ListAsync(AdminUserListQuery query, CancellationToken cancellationToken)
    {
        var users = store.Database.GetCollection<UserDocument>("users");
        var filter = Builders<UserDocument>.Filter.Empty;
        if (query.Search is not null)
            filter &= Builders<UserDocument>.Filter.Regex(user => user.DisplayName,
                new BsonRegularExpression(Regex.Escape(query.Search), "i"));
        if (query.Role is not null) filter &= Builders<UserDocument>.Filter.AnyEq(user => user.Roles, query.Role);
        if (query.AccountStatus is not null)
            filter &= Builders<UserDocument>.Filter.Eq(user => user.AccountStatus, query.AccountStatus);

        var sort = query.CreatedAtDescending
            ? Builders<UserDocument>.Sort.Descending(user => user.CreatedAt).Ascending(user => user.Id)
            : Builders<UserDocument>.Sort.Ascending(user => user.CreatedAt).Ascending(user => user.Id);
        var total = await users.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var page = await users.Find(filter).Sort(sort).Skip(checked((query.Page - 1) * query.PageSize))
            .Limit(query.PageSize).ToListAsync(cancellationToken);
        return new AdminUserPage(page.Select(user => new AdminUserListEntry(user.Id.ToString(),
            user.DisplayName, user.Roles, user.AccountStatus, user.CreatedAt)).ToArray(), total);
    }
}
