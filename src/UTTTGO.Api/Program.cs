using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using StackExchange.Redis;
using UTTTGO.Api.Modules.Admin.Application;
using UTTTGO.Api.Modules.Admin.Contracts;
using UTTTGO.Api.Modules.Admin.Infrastructure;
using UTTTGO.Api.Modules.Admin.Validators;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Identity.Contracts;
using UTTTGO.Api.Modules.Identity.Infrastructure;
using UTTTGO.Api.Modules.Identity.Validators;
using UTTTGO.Api.Modules.Sellers.Application;
using UTTTGO.Api.Modules.Sellers.Contracts;
using UTTTGO.Api.Modules.Sellers.Infrastructure;
using UTTTGO.Api.Modules.Sellers.Validators;
using UTTTGO.Api.Shared.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers(options => options.Filters.Add<LoginRateLimitPartitionFilter>());
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<MongoOptions>(options =>
{
    options.Uri = builder.Configuration["MONGODB_URI"];
    var name = builder.Configuration["MONGODB_DATABASE"];
    options.Database = string.IsNullOrWhiteSpace(name) ? "utttgo" : name;
});
builder.Services.AddSingleton<IMongoClient>(services =>
{
    var uri = services.GetRequiredService<IOptions<MongoOptions>>().Value.Uri;
    if (string.IsNullOrWhiteSpace(uri)) throw new ExternalDependencyUnavailableException();
    var settings = MongoClientSettings.FromConnectionString(uri);
    settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
    return new MongoClient(settings);
});
builder.Services.AddSingleton<MongoStore>();
builder.Services.AddHostedService<IdentityMongoIndexes>();
builder.Services.Configure<RedisOptions>(options => options.Url = builder.Configuration["REDIS_URL"]);
builder.Services.Configure<RateLimitingOptions>(builder.Configuration.GetSection("RateLimiting"));
builder.Services.Configure<SupabaseJwtOptions>(options =>
{
    options.JwksUrl = builder.Configuration["SUPABASE_JWKS_URL"];
    options.Secret = builder.Configuration["SUPABASE_JWT_SECRET"];
    options.Issuer = builder.Configuration["SUPABASE_JWT_ISSUER"];
    options.Audience = builder.Configuration["SUPABASE_JWT_AUDIENCE"];
});
builder.Services.Configure<SupabaseOptions>(options =>
{
    options.Url = builder.Configuration["SUPABASE_URL"];
    options.AnonKey = builder.Configuration["SUPABASE_ANON_KEY"];
});
builder.Services.AddHttpClient<SupabaseAuthClient>(client => client.Timeout = TimeSpan.FromSeconds(10))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddSingleton<IConnectionMultiplexer>(services =>
{
    var url = services.GetRequiredService<IOptions<RedisOptions>>().Value.Url;
    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
        uri.Scheme is not ("redis" or "rediss") || uri.Host.Length == 0 ||
        uri.Query.Length != 0 || uri.Fragment.Length != 0)
        throw new ExternalDependencyUnavailableException();

    var configuration = new ConfigurationOptions
    {
        AbortOnConnectFail = false,
        ConnectTimeout = 2000,
        SyncTimeout = 2000,
        Ssl = uri.Scheme == "rediss",
        SslHost = uri.Host,
        User = uri.UserInfo.Split(':', 2)[0] is { Length: > 0 } user ? Uri.UnescapeDataString(user) : null,
        Password = uri.UserInfo.Contains(':') ? Uri.UnescapeDataString(uri.UserInfo.Split(':', 2)[1]) : null
    };
    configuration.EndPoints.Add(uri.Host, uri.IsDefaultPort ? (configuration.Ssl ? 6380 : 6379) : uri.Port);
    if (uri.AbsolutePath != "/")
    {
        if (!int.TryParse(uri.AbsolutePath.Trim('/'), out var database) || database < 0)
            throw new ExternalDependencyUnavailableException();
        configuration.DefaultDatabase = database;
    }
    return ConnectionMultiplexer.Connect(configuration);
});
builder.Services.AddSingleton<RedisRateLimiter>();
builder.Services.AddSingleton(new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
{
    Timeout = TimeSpan.FromSeconds(5)
});
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context => new BadRequestObjectResult(
        ApiError.Create(context.HttpContext, 400, "INVALID_REQUEST", "Solicitud inválida."));
});
builder.Services.AddScoped<IValidator<RegisterRequest>, RegisterRequestValidator>();
builder.Services.AddScoped<IRegistrationService, RegistrationService>();
builder.Services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
builder.Services.AddScoped<ILoginService, LoginService>();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<IOwnProfileService, OwnProfileService>();
builder.Services.AddScoped<IUpdateOwnProfileService, UpdateOwnProfileService>();
builder.Services.AddScoped<IValidator<UpdateOwnProfileRequest>, UpdateOwnProfileRequestValidator>();
builder.Services.AddSingleton<MongoUserRepositories>();
builder.Services.AddSingleton<IProfileUpdateRepository>(services => services.GetRequiredService<MongoUserRepositories>());
builder.Services.AddSingleton<IApprovedAvatarReadAccess, UnconfiguredApprovedAvatarReadAccess>();
builder.Services.AddScoped<IAuthRateLimitGate, RedisAuthRateLimitGate>();
builder.Services.AddScoped<ISupabaseRegistration>(services => services.GetRequiredService<SupabaseAuthClient>());
builder.Services.AddSingleton<IUserRegistrationRepository>(services => services.GetRequiredService<MongoUserRepositories>());
builder.Services.AddScoped<ISupabaseLogin>(services => services.GetRequiredService<SupabaseAuthClient>());
builder.Services.AddSingleton<ILoginProfileRepository>(services => services.GetRequiredService<MongoUserRepositories>());
builder.Services.AddScoped<ILoginJwtGate, RedisJwtGate>();
builder.Services.AddScoped<IValidatedJwtSubjectReader, ValidatedJwtSubjectReader>();
builder.Services.AddScoped<ISupabaseSessionIdentity>(services => services.GetRequiredService<SupabaseAuthClient>());
builder.Services.AddScoped<IAdminUsersService, AdminUsersService>();
builder.Services.AddScoped<IValidator<AdminUsersQueryRequest>, AdminUsersQueryRequestValidator>();
builder.Services.AddSingleton<IAdminUserListReader, MongoAdminUserListReader>();
builder.Services.AddScoped<IAdminUsersRateLimitGate, RedisAdminUsersRateLimitGate>();
builder.Services.AddSingleton<IAdminUsersAudit, UnconfiguredAdminUsersAudit>();
builder.Services.AddScoped<IValidator<SellerIdRequest>, SellerIdRequestValidator>();
builder.Services.AddScoped<ISellerProfileService, SellerProfileService>();
builder.Services.AddScoped<IPublicSellerRateLimitGate, RedisPublicSellerRateLimitGate>();
builder.Services.AddSingleton<ISellerProfileRepository, MongoSellerProfileRepository>();
builder.Services.AddSingleton<ISellerRatingsReader, UnconfiguredSellerRatingsReader>();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
