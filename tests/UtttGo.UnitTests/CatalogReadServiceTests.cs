using FluentAssertions;
using UTTTGO.Api.Modules.Catalog.Application;
using UTTTGO.Api.Modules.Catalog.Contracts;
using Xunit;

namespace UtttGo.UnitTests;

public sealed class CatalogReadServiceTests
{
    [Fact]
    public async Task PublicListingPublishesProductsWithPaginationAndNoInventedRatings()
    {
        var products = new CatalogProductRepository();
        products.Products[CatalogFixtures.ProductId] = CatalogFixtures.Product();
        var categories = new CatalogCategoryRepository();
        categories.Categories[CatalogFixtures.CategoryId] = CatalogFixtures.Category();
        var sellers = new CatalogSellerNames();
        sellers.Names[CatalogFixtures.SellerId] = "Campus Snacks";

        var result = await Service(products, categories, sellers).ListAsync(CatalogFixtures.Query(), default);

        result.Outcome.Should().Be(ProductListOutcome.Success);
        result.Response!.Items.Should().HaveCount(1);
        result.Response.TotalCount.Should().Be(1);
        result.Response.Page.Should().Be(1);
        result.Response.PageSize.Should().Be(20);
        var item = result.Response.Items[0];
        item.Id.Should().Be(CatalogFixtures.ProductId);
        item.SellerDisplayName.Should().Be("Campus Snacks");
        item.CategoryName.Should().Be("Comida");
        item.Price.Should().Be(55.50m);
        item.IsAvailable.Should().BeTrue();
        item.RatingAverage.Should().BeNull();
        item.RatingCount.Should().BeNull();
    }

    [Fact]
    public async Task ListingOnlyPublishesProductsOfActiveCategories()
    {
        var products = new CatalogProductRepository();
        products.Products[CatalogFixtures.ProductId] = CatalogFixtures.Product();
        var categories = new CatalogCategoryRepository();
        categories.Categories[CatalogFixtures.CategoryId] = CatalogFixtures.Category(isActive: false);

        var result = await Service(products, categories, new CatalogSellerNames())
            .ListAsync(CatalogFixtures.Query(), default);

        result.Outcome.Should().Be(ProductListOutcome.Success);
        result.Response!.Items.Should().BeEmpty();
        products.LastQuery!.AllowedCategoryIds.Should().BeEmpty();
    }

    [Fact]
    public async Task RateLimitDenialSkipsTheCatalogQuery()
    {
        var products = new CatalogProductRepository();
        var result = await new ListProductsService(new CatalogRateLimitGate { Allowed = false }, products,
            new CatalogCategoryRepository(), new CatalogRatingsReader(), new CatalogSellerNames(), new CatalogImageGuard())
            .ListAsync(CatalogFixtures.Query(), default);

        result.Outcome.Should().Be(ProductListOutcome.RateLimited);
        products.ListCalls.Should().Be(0);
    }

    [Fact]
    public async Task CriticalFailureIsUnavailableWithoutDetails()
    {
        var result = await new ListProductsService(new CatalogRateLimitGate(),
            new CatalogProductRepository { Fail = true }, new CatalogCategoryRepository(),
            new CatalogRatingsReader(), new CatalogSellerNames(), new CatalogImageGuard())
            .ListAsync(CatalogFixtures.Query(), default);

        result.Outcome.Should().Be(ProductListOutcome.Unavailable);
        result.Response.Should().BeNull();
    }

    [Fact]
    public async Task ReviewsFailureDegradesToAbsentRatings()
    {
        var products = new CatalogProductRepository();
        products.Products[CatalogFixtures.ProductId] = CatalogFixtures.Product();
        var categories = new CatalogCategoryRepository();
        categories.Categories[CatalogFixtures.CategoryId] = CatalogFixtures.Category();

        var result = await new ListProductsService(new CatalogRateLimitGate(), products, categories,
            new CatalogRatingsReader { Fail = true }, new CatalogSellerNames(), new CatalogImageGuard())
            .ListAsync(CatalogFixtures.Query(), default);

        result.Outcome.Should().Be(ProductListOutcome.Success);
        result.Response!.Items[0].RatingAverage.Should().BeNull();
    }

    [Fact]
    public async Task PublishedRatingsAreOnlyUsedWhenReviewsProvideThem()
    {
        var products = new CatalogProductRepository();
        products.Products[CatalogFixtures.ProductId] = CatalogFixtures.Product();
        var categories = new CatalogCategoryRepository();
        categories.Categories[CatalogFixtures.CategoryId] = CatalogFixtures.Category();

        var result = await new ListProductsService(new CatalogRateLimitGate(), products, categories,
            new CatalogRatingsReader { Snapshot = new(4.5m, 12) }, new CatalogSellerNames(), new CatalogImageGuard())
            .ListAsync(CatalogFixtures.Query(), default);

        result.Response!.Items[0].RatingAverage.Should().Be(4.5m);
        result.Response.Items[0].RatingCount.Should().Be(12);
    }

    [Fact]
    public async Task PublicDetailReturnsTheProductAndHidesPausedOnes()
    {
        var products = new CatalogProductRepository();
        products.Products[CatalogFixtures.ProductId] = CatalogFixtures.Product();
        var categories = new CatalogCategoryRepository();
        categories.Categories[CatalogFixtures.CategoryId] = CatalogFixtures.Category();
        var service = new GetProductService(new CatalogRateLimitGate(), products, categories,
            new CatalogRatingsReader(), new CatalogSellerNames(), new CatalogImageGuard());

        var found = await service.GetAsync(CatalogFixtures.ProductId, default);
        found.Outcome.Should().Be(GetProductOutcome.Found);
        found.Response!.Name.Should().Be("Tacos de pastor");
        found.Response.CategoryName.Should().Be("Comida");

        products.Products[CatalogFixtures.ProductId] = CatalogFixtures.Product(status: "Paused");
        var paused = await service.GetAsync(CatalogFixtures.ProductId, default);
        paused.Outcome.Should().Be(GetProductOutcome.NotFound);
        paused.Response.Should().BeNull();
    }

    [Fact]
    public async Task PublicDetailHidesProductsOfInactiveCategories()
    {
        var products = new CatalogProductRepository();
        products.Products[CatalogFixtures.ProductId] = CatalogFixtures.Product();
        var categories = new CatalogCategoryRepository();
        categories.Categories[CatalogFixtures.CategoryId] = CatalogFixtures.Category(isActive: false);

        var result = await new GetProductService(new CatalogRateLimitGate(), products, categories,
            new CatalogRatingsReader(), new CatalogSellerNames(), new CatalogImageGuard())
            .GetAsync(CatalogFixtures.ProductId, default);

        result.Outcome.Should().Be(GetProductOutcome.NotFound);
    }

    [Fact]
    public async Task PublicCategoriesArePaginatedAndOnlyActive()
    {
        var categories = new CatalogCategoryRepository();
        categories.Categories[CatalogFixtures.CategoryId] = CatalogFixtures.Category();
        categories.Categories["507f1f77bcf86cd799439032"] = new("507f1f77bcf86cd799439032", "Bebidas",
            false, 2);

        var result = await new ListCategoriesService(new CatalogRateLimitGate(), categories)
            .ListAsync(1, 20, default);

        result.Outcome.Should().Be(CategoryListOutcome.Success);
        result.Response!.Items.Should().HaveCount(1);
        result.Response.Items[0].Name.Should().Be("Comida");
        result.Response.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task CategoryFailureIsUnavailable()
    {
        var result = await new ListCategoriesService(new CatalogRateLimitGate(),
            new CatalogCategoryRepository { Fail = true }).ListAsync(1, 20, default);

        result.Outcome.Should().Be(CategoryListOutcome.Unavailable);
    }

    private static ListProductsService Service(CatalogProductRepository products,
        CatalogCategoryRepository categories, CatalogSellerNames sellers) =>
        new(new CatalogRateLimitGate(), products, categories, new CatalogRatingsReader(), sellers,
            new CatalogImageGuard());
}
