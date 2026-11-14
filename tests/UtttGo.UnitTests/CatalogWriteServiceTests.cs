using FluentAssertions;
using UTTTGO.Api.Modules.Catalog.Application;
using UTTTGO.Api.Modules.Catalog.Contracts;
using UTTTGO.Api.Modules.Identity.Application;
using Xunit;

namespace UtttGo.UnitTests;

public sealed class CatalogWriteServiceTests
{
    private static readonly CreateProductRequest ValidRequest = new()
    {
        Name = "Tacos de pastor",
        Description = "Tacos con piña",
        Ingredients = ["Piña", "Pastor"],
        CategoryId = CatalogFixtures.CategoryId,
        Price = 55.50m,
        Stock = 10
    };

    [Fact]
    public async Task SellerCreatesItsOwnProductWithoutChoosingOwnership()
    {
        var products = new CatalogProductRepository();

        var result = await CreateService(SellerProfile(), products, ActiveCategories())
            .CreateAsync("token", ValidRequest, default);

        result.Outcome.Should().Be(CreateProductOutcome.Created);
        result.Response!.SellerId.Should().Be(CatalogFixtures.SellerId);
        result.Response.Status.Should().Be("Active");
        result.Response.Price.Should().Be(55.50m);
        result.Response.IsAvailable.Should().BeTrue();
        products.LastSellerId.Should().Be(CatalogFixtures.SellerId);
    }

    [Fact]
    public async Task BuyerCannotPublishProducts()
    {
        var profiles = new AuthenticatedProfileDouble { Profile = SellerFixtures.Buyer("Active") };

        var result = await CreateService(profiles, new CatalogProductRepository(),
            new CatalogCategoryRepository()).CreateAsync("token", ValidRequest, default);

        result.Outcome.Should().Be(CreateProductOutcome.NotSeller);
    }

    [Fact]
    public async Task UnauthenticatedRequestIsRejected()
    {
        var profiles = new AuthenticatedProfileDouble
        {
            Outcome = AuthenticatedLocalProfileOutcome.Unauthorized
        };

        var result = await CreateService(profiles, new CatalogProductRepository(),
            new CatalogCategoryRepository()).CreateAsync("token", ValidRequest, default);

        result.Outcome.Should().Be(CreateProductOutcome.Unauthorized);
    }

    [Fact]
    public async Task NonActiveAccountCannotPublish()
    {
        var profiles = new AuthenticatedProfileDouble
        {
            Profile = SellerFixtures.Buyer("Suspended", CatalogFixtures.SellerId, true)
        };

        var result = await CreateService(profiles, new CatalogProductRepository(),
            new CatalogCategoryRepository()).CreateAsync("token", ValidRequest, default);

        result.Outcome.Should().Be(CreateProductOutcome.AccountNotActive);
    }

    [Fact]
    public async Task UnknownOrInactiveCategoryIsRejected()
    {
        var categories = new CatalogCategoryRepository();
        categories.Categories[CatalogFixtures.CategoryId] = CatalogFixtures.Category(isActive: false);

        var result = await CreateService(SellerProfile(), new CatalogProductRepository(), categories)
            .CreateAsync("token", ValidRequest, default);

        result.Outcome.Should().Be(CreateProductOutcome.CategoryNotFound);
    }

    [Fact]
    public async Task RateLimitDenialSkipsTheWrite()
    {
        var products = new CatalogProductRepository();

        var result = await CreateService(SellerProfile(), products, new CatalogCategoryRepository(),
            new CatalogRateLimitGate { Allowed = false }).CreateAsync("token", ValidRequest, default);

        result.Outcome.Should().Be(CreateProductOutcome.RateLimited);
        products.CreateCalls.Should().Be(0);
    }

    [Fact]
    public async Task RepositoryThatChangesOwnershipIsTreatedAsUnavailable()
    {
        var products = new CatalogProductRepository { ForceTampered = true };

        var result = await CreateService(SellerProfile(), products, ActiveCategories())
            .CreateAsync("token", ValidRequest, default);

        result.Outcome.Should().Be(CreateProductOutcome.Unavailable);
    }

    [Fact]
    public async Task OwnerUpdatesItsProductAndCompetesByVersion()
    {
        var products = new CatalogProductRepository();
        products.Products[CatalogFixtures.ProductId] = CatalogFixtures.Product();
        var request = new UpdateProductRequest(true, "Tacos de pastor premium", false, null, false, null,
            true, 60m, false, null);

        var result = await UpdateService(SellerProfile(), products, ActiveCategories())
            .UpdateAsync("token", CatalogFixtures.ProductId, request, default);

        result.Outcome.Should().Be(UpdateProductOutcome.Updated);
        result.Response!.Name.Should().Be("Tacos de pastor premium");
        result.Response.Price.Should().Be(60m);
        products.UpdateCalls.Should().Be(1);
    }

    [Fact]
    public async Task ProductOfAnotherSellerIsReportedAsMissing()
    {
        var products = new CatalogProductRepository();
        products.Products[CatalogFixtures.ProductId] = CatalogFixtures.Product(CatalogFixtures.OtherSellerId);
        var request = new UpdateProductRequest(true, "Intruso", false, null, false, null, false, null,
            false, null);

        var result = await UpdateService(SellerProfile(), products, ActiveCategories())
            .UpdateAsync("token", CatalogFixtures.ProductId, request, default);

        result.Outcome.Should().Be(UpdateProductOutcome.NotFound);
        products.UpdateCalls.Should().Be(0);
    }

    [Fact]
    public async Task ConcurrentUpdateIsReportedAsConflict()
    {
        var products = new CatalogProductRepository { ForceConflict = true };
        products.Products[CatalogFixtures.ProductId] = CatalogFixtures.Product();
        var request = new UpdateProductRequest(false, null, true, "Otro texto", false, null, false, null,
            false, null);

        var result = await UpdateService(SellerProfile(), products, ActiveCategories())
            .UpdateAsync("token", CatalogFixtures.ProductId, request, default);

        result.Outcome.Should().Be(UpdateProductOutcome.Conflict);
    }

    [Fact]
    public async Task AvailabilityPausesThePublicationWithoutTouchingStock()
    {
        var products = new CatalogProductRepository();
        products.Products[CatalogFixtures.ProductId] = CatalogFixtures.Product();
        var request = new UpdateProductAvailabilityRequest { Availability = "Paused" };

        var result = await AvailabilityService(SellerProfile(), products, ActiveCategories())
            .UpdateAsync("token", CatalogFixtures.ProductId, request, default);

        result.Outcome.Should().Be(UpdateProductAvailabilityOutcome.Updated);
        result.Response!.Status.Should().Be("Paused");
        result.Response.Stock.Should().Be(10);
        result.Response.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task AvailabilityThatChangesStockIsTreatedAsUnavailable()
    {
        var products = new CatalogProductRepository { ForceTampered = true };
        products.Products[CatalogFixtures.ProductId] = CatalogFixtures.Product();
        var request = new UpdateProductAvailabilityRequest { Availability = "Paused" };

        var result = await AvailabilityService(SellerProfile(), products, ActiveCategories())
            .UpdateAsync("token", CatalogFixtures.ProductId, request, default);

        result.Outcome.Should().Be(UpdateProductAvailabilityOutcome.Unavailable);
    }

    [Fact]
    public async Task BuyerCannotChangeAvailability()
    {
        var profiles = new AuthenticatedProfileDouble { Profile = SellerFixtures.Buyer("Active") };
        var request = new UpdateProductAvailabilityRequest { Availability = "Paused" };

        var result = await AvailabilityService(profiles, new CatalogProductRepository(), ActiveCategories())
            .UpdateAsync("token", CatalogFixtures.ProductId, request, default);

        result.Outcome.Should().Be(UpdateProductAvailabilityOutcome.NotSeller);
    }

    private static AuthenticatedProfileDouble SellerProfile() =>
        new() { Profile = SellerFixtures.Buyer("Active", CatalogFixtures.SellerId, true) };

    private static CatalogCategoryRepository ActiveCategories()
    {
        var categories = new CatalogCategoryRepository();
        categories.Categories[CatalogFixtures.CategoryId] = CatalogFixtures.Category();
        return categories;
    }

    private static CreateProductService CreateService(AuthenticatedProfileDouble profiles,
        CatalogProductRepository products, CatalogCategoryRepository categories,
        CatalogRateLimitGate? rateLimit = null) =>
        new(profiles, rateLimit ?? new CatalogRateLimitGate(), categories, products, new CatalogSellerNames());

    private static UpdateProductService UpdateService(AuthenticatedProfileDouble profiles,
        CatalogProductRepository products, CatalogCategoryRepository categories,
        CatalogRateLimitGate? rateLimit = null) =>
        new(profiles, rateLimit ?? new CatalogRateLimitGate(), products, categories);

    private static UpdateProductAvailabilityService AvailabilityService(AuthenticatedProfileDouble profiles,
        CatalogProductRepository products, CatalogCategoryRepository categories,
        CatalogRateLimitGate? rateLimit = null) =>
        new(profiles, rateLimit ?? new CatalogRateLimitGate(), products, categories);
}
