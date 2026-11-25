using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using StackExchange.Redis;
using UTTTGO.Api.Modules.Admin.Application;
using UTTTGO.Api.Modules.Admin.Contracts;
using UTTTGO.Api.Modules.Admin.Infrastructure;
using UTTTGO.Api.Modules.Admin.Validators;
using UTTTGO.Api.Modules.Catalog.Application;
using UTTTGO.Api.Modules.Catalog.Contracts;
using UTTTGO.Api.Modules.Catalog.Infrastructure;
using UTTTGO.Api.Modules.Catalog.Validators;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Identity.Contracts;
using UTTTGO.Api.Modules.Identity.Infrastructure;
using UTTTGO.Api.Modules.Identity.Validators;
using UTTTGO.Api.Modules.Orders.Application;
using UTTTGO.Api.Modules.Orders.Contracts;
using UTTTGO.Api.Modules.Orders.Infrastructure;
using UTTTGO.Api.Modules.Orders.Validators;
using UTTTGO.Api.Modules.Sellers.Application;
using UTTTGO.Api.Modules.Sellers.Contracts;
using UTTTGO.Api.Modules.Sellers.Infrastructure;
using UTTTGO.Api.Modules.Sellers.Validators;
using UTTTGO.Api.Shared.Contracts;
using UTTTGO.Api.Shared.Health;
using UTTTGO.Api.Shared.Infrastructure;
using UTTTGO.Api.Modules.Media.Application;
using UTTTGO.Api.Modules.Media.Contracts;
using Amazon;
using Amazon.Rekognition;
using UTTTGO.Api.Modules.Media.Infrastructure;
using UTTTGO.Api.Modules.Media.Validators;
using UTTTGO.Api.Modules.Reviews.Application;
using UTTTGO.Api.Modules.Reviews.Contracts;
using UTTTGO.Api.Modules.Reviews.Infrastructure;
using UTTTGO.Api.Modules.Reviews.Validators;
using UTTTGO.Api.Modules.Notifications.Application;
using UTTTGO.Api.Modules.Notifications.Contracts;
using UTTTGO.Api.Modules.Notifications.Infrastructure;
using UTTTGO.Api.Modules.Notifications.Validators;
using UTTTGO.Api.Shared.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers(options => options.Filters.Add<LoginRateLimitPartitionFilter>());
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = ["application/json", "application/problem+json"];
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options => options.Level = System.IO.Compression.CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(options => options.Level = System.IO.Compression.CompressionLevel.Fastest);
builder.Services.AddDataProtection();
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
builder.Services.Configure<CatalogSeedOptions>(options =>
    options.Enabled = bool.TryParse(builder.Configuration["CATALOG_SEED_ENABLED"], out var enabled) && enabled);
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
builder.Services.AddHttpClient("SupabaseLogout", client => client.Timeout = TimeSpan.FromSeconds(3))
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
builder.Services.AddScoped<IDeleteOwnAccountService, DeleteOwnAccountService>();
builder.Services.AddScoped<IValidator<UpdateOwnProfileRequest>, UpdateOwnProfileRequestValidator>();
builder.Services.AddSingleton<MongoUserRepositories>();
builder.Services.AddSingleton<IProfileUpdateRepository>(services => services.GetRequiredService<MongoUserRepositories>());
builder.Services.AddSingleton<IAccountDeletionRepository>(services => services.GetRequiredService<MongoUserRepositories>());
builder.Services.AddSingleton<IApprovedAvatarReadAccess, UnconfiguredApprovedAvatarReadAccess>();
builder.Services.AddScoped<IAuthRateLimitGate, RedisAuthRateLimitGate>();
builder.Services.AddScoped<ISupabaseRegistration>(services => services.GetRequiredService<SupabaseAuthClient>());
builder.Services.AddSingleton<IUserRegistrationRepository>(services => services.GetRequiredService<MongoUserRepositories>());
builder.Services.AddScoped<ISupabaseLogin>(services => services.GetRequiredService<SupabaseAuthClient>());
builder.Services.AddSingleton<ILoginProfileRepository>(services => services.GetRequiredService<MongoUserRepositories>());
builder.Services.AddScoped<ILoginJwtGate, RedisJwtGate>();
builder.Services.AddScoped<ILogoutService, LogoutService>();
builder.Services.AddScoped<ISupabaseSessionRevoker, SupabaseSessionRevoker>();
builder.Services.AddSingleton<IJwtRevocationStore, RedisJwtRevocationStore>();
builder.Services.AddScoped<IValidatedJwtSubjectReader, ValidatedJwtSubjectReader>();
builder.Services.AddScoped<ISupabaseSessionIdentity>(services => services.GetRequiredService<SupabaseAuthClient>());
builder.Services.AddScoped<IAdminUsersService, AdminUsersService>();
builder.Services.AddScoped<IValidator<AdminUsersQueryRequest>, AdminUsersQueryRequestValidator>();
builder.Services.AddSingleton<IAdminUserListReader, MongoAdminUserListReader>();
builder.Services.AddSingleton<IMongoHealthProbe, MongoHealthProbe>();
builder.Services.AddSingleton<IRedisHealthProbe, RedisHealthProbe>();
builder.Services.AddSingleton<IHealthService, HealthService>();
builder.Services.AddScoped<IHealthRateLimitGate, RedisHealthRateLimitGate>();
builder.Services.AddScoped<IAdminUsersRateLimitGate, RedisAdminUsersRateLimitGate>();
builder.Services.AddSingleton<IAdminUsersAudit, UnconfiguredAdminUsersAudit>();
builder.Services.AddScoped<IValidator<SellerIdRequest>, SellerIdRequestValidator>();
builder.Services.AddScoped<ISellerProfileService, SellerProfileService>();
builder.Services.AddScoped<IPublicSellerRateLimitGate, RedisPublicSellerRateLimitGate>();
builder.Services.AddSingleton<ISellerProfileRepository, MongoSellerProfileRepository>();
builder.Services.AddSingleton<ISellerRatingsReader, UnconfiguredSellerRatingsReader>();
builder.Services.AddScoped<IAuthenticatedLocalProfileReader, AuthenticatedLocalProfileReader>();
builder.Services.AddSingleton<ISellerRolePromotion, MongoSellerRolePromotion>();
builder.Services.AddScoped<ISellerRateLimitGate, RedisSellerRateLimitGate>();
builder.Services.AddSingleton<ISellerProfileWriter, MongoSellerProfileWriter>();
builder.Services.AddScoped<ISellerActivationService, SellerActivationService>();
builder.Services.AddScoped<IOwnSellerProfileService, OwnSellerProfileService>();
builder.Services.AddScoped<IUpdateOwnSellerProfileService, UpdateOwnSellerProfileService>();
builder.Services.AddScoped<IValidator<SellerActivationRequest>, SellerActivationRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateSellerProfileRequest>, UpdateSellerProfileRequestValidator>();
builder.Services.AddHostedService<SellersMongoIndexes>();
builder.Services.AddSingleton<ISellerDisplayNames, MongoSellerDisplayNames>();
builder.Services.AddScoped<IValidator<ProductListQuery>, ProductListQueryValidator>();
builder.Services.AddScoped<IValidator<CreateProductRequest>, CreateProductRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateProductRequest>, UpdateProductRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateProductAvailabilityRequest>, UpdateProductAvailabilityRequestValidator>();
builder.Services.AddSingleton<MongoProductRepository>();
builder.Services.AddSingleton<IProductRepository>(services =>
    services.GetRequiredService<MongoProductRepository>());
builder.Services.AddSingleton<IAccountDeletionCatalog>(services =>
    services.GetRequiredService<MongoProductRepository>());
builder.Services.AddSingleton<ICategoryRepository, MongoCategoryRepository>();
builder.Services.AddScoped<ICatalogRateLimitGate, RedisCatalogRateLimitGate>();
builder.Services.AddSingleton<ICatalogCache, RedisCatalogCache>();
builder.Services.AddSingleton<IProductRatingsReader, UnconfiguredProductRatingsReader>();
builder.Services.AddScoped<IListProductsService, ListProductsService>();
builder.Services.AddScoped<IListOwnProductsService, ListOwnProductsService>();
builder.Services.AddScoped<IGetProductService, GetProductService>();
builder.Services.AddScoped<IListCategoriesService, ListCategoriesService>();
builder.Services.AddScoped<ICreateProductService, CreateProductService>();
builder.Services.AddScoped<IUpdateProductService, UpdateProductService>();
builder.Services.AddScoped<IUpdateProductAvailabilityService, UpdateProductAvailabilityService>();
builder.Services.AddHostedService<CatalogMongoIndexes>();
builder.Services.AddSingleton<ICatalogSeeder, MongoCatalogSeeder>();
builder.Services.AddHostedService<CatalogSeedHostedService>();
builder.Services.AddSingleton<ICatalogOrderProducts, MongoOrderProductRepository>();
builder.Services.AddSingleton<ISellerOrderProfiles, MongoSellerOrderProfiles>();
builder.Services.Configure<OrderLimitsOptions>(builder.Configuration.GetSection("Orders"));
builder.Services.AddScoped<IValidator<CreateOrderRequest>, CreateOrderRequestValidator>();
builder.Services.Configure<S3Config>(options =>
{
    options.BucketName = builder.Configuration["S3_BUCKET_IMAGES"] ?? "";
    options.Region = builder.Configuration["AWS_REGION"] ?? "us-east-2";
});
builder.Services.AddSingleton<S3MediaObjectStore>();
builder.Services.AddSingleton<IMediaObjectStore>(services => services.GetRequiredService<S3MediaObjectStore>());
builder.Services.AddSingleton<IMediaImageSource>(services => services.GetRequiredService<S3MediaObjectStore>());
builder.Services.AddSingleton<IMediaRepository, MongoMediaRepository>();
builder.Services.Configure<RekognitionModerationOptions>(options =>
{
    if (float.TryParse(builder.Configuration["MODERATION_MIN_CONFIDENCE"], out var confidence))
        options.MinConfidence = confidence;
});
builder.Services.AddSingleton<IAmazonRekognition>(_ =>
    new AmazonRekognitionClient(RegionEndpoint.GetBySystemName(builder.Configuration["AWS_REGION"] ?? "us-east-2")));
builder.Services.AddScoped<IImageModerator, RekognitionImageModerator>();
builder.Services.AddScoped<MediaService>();
builder.Services.AddScoped<IProductMediaAccess>(services => services.GetRequiredService<MediaService>());
builder.Services.AddScoped<IValidator<PresignedUrlRequest>, PresignedUrlRequestValidator>();
builder.Services.AddScoped<IValidator<ConfirmRequest>, ConfirmRequestValidator>();
builder.Services.AddScoped<IValidator<OrderListQuery>, OrderListQueryValidator>();
builder.Services.AddScoped<IValidator<UpdateOrderStatusRequest>, UpdateOrderStatusRequestValidator>();
builder.Services.AddScoped<IValidator<DeliveryConfirmationRequest>, DeliveryConfirmationRequestValidator>();
builder.Services.AddSingleton<MongoOrderRepository>();
builder.Services.AddSingleton<IOrderRepository>(services =>
    services.GetRequiredService<MongoOrderRepository>());
builder.Services.AddSingleton<IActiveOrderReader>(services => services.GetRequiredService<MongoOrderRepository>());
builder.Services.AddSingleton<IDeliveredOrderReader>(services => services.GetRequiredService<MongoOrderRepository>());
builder.Services.AddSingleton<IOrderAuditWriter, MongoOrderAuditWriter>();
builder.Services.AddScoped<IOrdersRateLimitGate, RedisOrdersRateLimitGate>();
builder.Services.AddScoped<ICreateOrderService, CreateOrderService>();
builder.Services.AddScoped<IListOwnOrdersService, ListOwnOrdersService>();
builder.Services.AddScoped<IListSellerOrdersService, ListSellerOrdersService>();
builder.Services.AddScoped<IGetOrderService, GetOrderService>();
builder.Services.AddScoped<ICancelOrderService, CancelOrderService>();
builder.Services.AddScoped<IUpdateOrderStatusService, UpdateOrderStatusService>();
builder.Services.AddScoped<ICreateDeliveryTokenService, CreateDeliveryTokenService>();
builder.Services.AddScoped<IConfirmDeliveryService, ConfirmDeliveryService>();
builder.Services.AddHostedService<OrdersMongoIndexes>();
builder.Services.AddScoped<IValidator<CreateReviewRequest>, CreateReviewRequestValidator>();
builder.Services.AddScoped<IValidator<SellerReviewsQuery>, SellerReviewsQueryValidator>();
builder.Services.AddSingleton<MongoReviewRepository>();
builder.Services.AddSingleton<IReviewRepository>(services => services.GetRequiredService<MongoReviewRepository>());
builder.Services.AddScoped<IReviewsRateLimitGate, RedisReviewsRateLimitGate>();
builder.Services.AddScoped<ICreateReviewService, CreateReviewService>();
builder.Services.AddScoped<ListSellerReviewsService>();
builder.Services.AddScoped<IListSellerReviewsService>(services => services.GetRequiredService<ListSellerReviewsService>());
builder.Services.AddScoped<ISellerRatingsReader>(services => services.GetRequiredService<ListSellerReviewsService>());
builder.Services.AddScoped<IProductRatingsReader>(services => services.GetRequiredService<ListSellerReviewsService>());
builder.Services.AddHostedService<ReviewsMongoIndexes>();
builder.Services.AddSingleton<IDeviceTokenStore, MongoDeviceTokenStore>();
builder.Services.AddScoped<IDeviceTokenService, DeviceTokenService>();
builder.Services.AddScoped<INotificationsRateLimitGate, RedisNotificationsRateLimitGate>();
builder.Services.AddScoped<IValidator<RegisterDeviceTokenRequest>, RegisterDeviceTokenRequestValidator>();
builder.Services.AddSingleton<INotificationSender, FirebaseAdminNotificationSender>();
builder.Services.AddSingleton<INotificationEventPublisher, MongoNotificationOutbox>();
builder.Services.AddHostedService<NotificationMongoIndexes>();
builder.Services.AddHostedService<NotificationOutboxDispatcher>();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<PublicApiServerTransformer>());

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapOpenApi();
app.MapGet("/openapi", () => Results.Content(OpenApiUiPage.Html, "text/html; charset=utf-8"));

app.UseResponseCompression();
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
