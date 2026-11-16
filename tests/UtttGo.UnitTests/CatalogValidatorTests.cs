using FluentAssertions;
using UTTTGO.Api.Modules.Catalog.Application;
using UTTTGO.Api.Modules.Catalog.Contracts;
using UTTTGO.Api.Modules.Catalog.Validators;
using Xunit;

namespace UtttGo.UnitTests;

public sealed class CatalogValidatorTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("tacos", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void SearchLengthIsBounded(string? search, bool expected)
    {
        var query = new ProductListQuery(search, null, null, null, 1, 20, ProductSort.CreatedAtDesc);
        new ProductListQueryValidator().Validate(query).IsValid.Should().Be(expected);
    }

    [Fact]
    public void SearchIsBoundedToOneHundredCharacters()
    {
        var long_ = new string('a', 101);
        var query = new ProductListQuery(long_, null, null, null, 1, 20, ProductSort.CreatedAtDesc);
        new ProductListQueryValidator().Validate(query).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("507f1f77bcf86cd799439021", true)]
    [InlineData("not-an-id", false)]
    [InlineData("507f1f77bcf86cd79943902g", false)]
    public void CategoryFilterAcceptsOnlyObjectIds(string categoryId, bool expected)
    {
        var query = new ProductListQuery(null, categoryId, null, null, 1, 20, ProductSort.CreatedAtDesc);
        new ProductListQueryValidator().Validate(query).IsValid.Should().Be(expected);
    }

    [Fact]
    public void PriceRangeCannotBeInverted()
    {
        var query = new ProductListQuery(null, null, 100m, 50m, 1, 20, ProductSort.CreatedAtDesc);
        new ProductListQueryValidator().Validate(query).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0, 20, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 51, false)]
    [InlineData(1, 50, true)]
    [InlineData(10_001, 20, false)]
    public void PaginationIsBounded(int page, int pageSize, bool expected)
    {
        var query = new ProductListQuery(null, null, null, null, page, pageSize, ProductSort.CreatedAtDesc);
        new ProductListQueryValidator().Validate(query).IsValid.Should().Be(expected);
    }

    [Theory]
    [InlineData("createdAtDesc", true)]
    [InlineData("priceAsc", true)]
    [InlineData("priceDesc", true)]
    [InlineData("nameAsc", true)]
    [InlineData("offers", false)]
    public void SortOptionsAreAList(string sortBy, bool expected)
    {
        var query = new ProductListQuery(null, null, null, null, 1, 20, sortBy);
        new ProductListQueryValidator().Validate(query).IsValid.Should().Be(expected);
    }

    [Fact]
    public void CreateRequestRejectsUnknownAndInvalidFields()
    {
        var validator = new CreateProductRequestValidator();
        validator.Validate(new CreateProductRequest
        {
            Name = "Tacos",
            Description = "Con piña",
            Ingredients = ["Piña"],
            CategoryId = CatalogFixtures.CategoryId,
            Price = 55.50m,
            Stock = 10
        }).IsValid.Should().BeTrue();

        validator.Validate(new CreateProductRequest
        {
            Name = "Tacos",
            Description = "Con piña",
            CategoryId = CatalogFixtures.CategoryId,
            Price = 55.50m,
            Stock = 10,
            UnknownFields = new() { ["sellerId"] = default }
        }).IsValid.Should().BeFalse();

        validator.Validate(new CreateProductRequest
        {
            Name = "Tacos",
            Description = "Con piña",
            CategoryId = CatalogFixtures.CategoryId,
            Price = 0m,
            Stock = 10
        }).IsValid.Should().BeFalse();

        validator.Validate(new CreateProductRequest
        {
            Name = "Tacos",
            Description = "Con piña",
            CategoryId = CatalogFixtures.CategoryId,
            Price = 55.50m,
            Stock = -1
        }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpdateRequestIsStrictAboutOmittedAndNullFields()
    {
        using var omitted = System.Text.Json.JsonDocument.Parse("{\"price\":10}");
        UpdateProductRequest.TryParse(omitted.RootElement, out var parsed).Should().BeTrue();
        parsed!.HasPrice.Should().BeTrue();
        parsed.HasName.Should().BeFalse();

        using var explicitNull = System.Text.Json.JsonDocument.Parse("{\"name\":null}");
        UpdateProductRequest.TryParse(explicitNull.RootElement, out _).Should().BeFalse();

        using var unknown = System.Text.Json.JsonDocument.Parse("{\"sellerId\":\"x\"}");
        UpdateProductRequest.TryParse(unknown.RootElement, out _).Should().BeFalse();

        using var empty = System.Text.Json.JsonDocument.Parse("{}");
        UpdateProductRequest.TryParse(empty.RootElement, out _).Should().BeFalse();
    }

    [Fact]
    public void AvailabilityAcceptsOnlyPublicationStates()
    {
        var validator = new UpdateProductAvailabilityRequestValidator();
        validator.Validate(new UpdateProductAvailabilityRequest { Status = "Paused" }).IsValid.Should().BeTrue();
        validator.Validate(new UpdateProductAvailabilityRequest { Status = "Active" }).IsValid.Should().BeTrue();
        validator.Validate(new UpdateProductAvailabilityRequest { Status = "SoldOut" }).IsValid.Should().BeFalse();
        validator.Validate(new UpdateProductAvailabilityRequest { Status = null }).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("Tacos de PASTOR", "tacos de pastor")]
    [InlineData("  Ajos  ", "ajos")]
    [InlineData("Café con leche", "cafe con leche")]
    public void NormalizationRemovesAccentsAndCollapsesSpaces(string value, string expected)
    {
        TextNormalizer.Normalize(value).Should().Be(expected);
    }
}
