using FluentAssertions;
using UTTTGO.Api.Modules.Catalog.Application;
using Xunit;

namespace UtttGo.UnitTests;

public sealed class ListOwnProductsServiceTests
{
    [Fact]
    public async Task ListsOnlyTheAuthenticatedSellersProductsAndExcludesDeleted()
    {
        var products = new CatalogProductRepository();
        products.Products[CatalogFixtures.ProductId] = CatalogFixtures.Product();
        products.Products["507f1f77bcf86cd799439022"] = CatalogFixtures.Product(SellerFixtures.SellerId,
            ProductAvailability.Paused);
        products.Products["507f1f77bcf86cd799439023"] = CatalogFixtures.Product(
            CatalogFixtures.OtherSellerId);
        products.Products["507f1f77bcf86cd799439024"] = CatalogFixtures.Product(SellerFixtures.SellerId,
            ProductAvailability.Deleted);

        var categories = new CatalogCategoryRepository();
        categories.Categories[CatalogFixtures.CategoryId] = CatalogFixtures.Category();
        var service = new ListOwnProductsService(
            new AuthenticatedProfileDouble
            {
                Profile = SellerFixtures.Buyer("Active", SellerFixtures.SellerId, true)
            },
            new CatalogRateLimitGate(), products, categories);

        var result = await service.ListAsync("token", 1, 20, default);

        result.Outcome.Should().Be(ListOwnProductsOutcome.Success);
        result.Response!.TotalCount.Should().Be(2);
        result.Response.Items.Should().OnlyContain(item => item.SellerId == SellerFixtures.SellerId);
        result.Response.Items.Should().NotContain(item => item.Status == ProductAvailability.Deleted);
    }

    [Fact]
    public async Task BuyerCannotListSellerProducts()
    {
        var service = new ListOwnProductsService(
            new AuthenticatedProfileDouble { Profile = SellerFixtures.Buyer() },
            new CatalogRateLimitGate(), new CatalogProductRepository(), new CatalogCategoryRepository());

        var result = await service.ListAsync("token", 1, 20, default);

        result.Outcome.Should().Be(ListOwnProductsOutcome.NotSeller);
    }
}
