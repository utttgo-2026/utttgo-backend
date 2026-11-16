using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using UTTTGO.Api.Modules.Orders.Application;
using UTTTGO.Api.Modules.Orders.Contracts;
using UTTTGO.Api.Modules.Orders.Validators;
using Xunit;

namespace UtttGo.UnitTests;

public sealed class OrderValidatorTests
{
    private static CreateOrderRequestValidator CreateValidator(int maxQuantity = 50) =>
        new(Options.Create(new OrderLimitsOptions { MaxQuantityPerOrder = maxQuantity }));

    [Fact]
    public async Task ValidRequestIsAccepted()
    {
        var request = new CreateOrderRequest { ProductId = OrderFixtures.ProductId, Quantity = 2 };

        (await CreateValidator().ValidateAsync(request)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-an-id")]
    [InlineData("507f1f77bcf86cd7994390")]
    public async Task InvalidProductIdIsRejected(string? productId)
    {
        var request = new CreateOrderRequest { ProductId = productId, Quantity = 1 };

        (await CreateValidator().ValidateAsync(request)).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(51)]
    public async Task QuantityOutsideTheAllowedRangeIsRejected(int quantity)
    {
        var request = new CreateOrderRequest { ProductId = OrderFixtures.ProductId, Quantity = quantity };

        (await CreateValidator().ValidateAsync(request)).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task QuantityAtTheConfiguredLimitIsAccepted()
    {
        var request = new CreateOrderRequest { ProductId = OrderFixtures.ProductId, Quantity = 50 };

        (await CreateValidator(maxQuantity: 50).ValidateAsync(request)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("price")]
    [InlineData("total")]
    [InlineData("unitPrice")]
    [InlineData("sellerId")]
    [InlineData("status")]
    [InlineData("paymentMethod")]
    [InlineData("currency")]
    public async Task PrivilegedFieldsFromTheClientAreRejected(string field)
    {
        var json = $$"""
            { "productId": "{{OrderFixtures.ProductId}}", "quantity": 1, "{{field}}": "any" }
            """;
        var request = System.Text.Json.JsonSerializer.Deserialize<CreateOrderRequest>(json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        (await CreateValidator().ValidateAsync(request!)).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 51)]
    public async Task InvalidPaginationIsRejected(int page, int pageSize)
    {
        (await new OrderListQueryValidator().ValidateAsync(new OrderListQuery(page, pageSize, null)))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task KnownStatusIsAcceptedAndUnknownStatusIsRejected()
    {
        (await new OrderListQueryValidator().ValidateAsync(new OrderListQuery(1, 20, "Pending")))
            .IsValid.Should().BeTrue();
        (await new OrderListQueryValidator().ValidateAsync(new OrderListQuery(1, 20, "Inventado")))
            .IsValid.Should().BeFalse();
    }
}
