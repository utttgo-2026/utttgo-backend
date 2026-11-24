using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace UtttGo.IntegrationTests;

public sealed class OpenApiContractTests
{
    [Fact]
    public async Task MediaAndCatalogImageContractsAppearInGeneratedDocument()
    {
        using var document = await LoadDocumentAsync();
        ResponseSchema(document, "/api/v1/media/upload-url", "post", 201, "PresignedUrlResponse");
        ResponseSchema(document, "/api/v1/media/confirm", "post", 200, "ConfirmResponse");
        RequestSchemaProperties(document, "/api/v1/media/confirm", "post").Should()
            .Contain("mediaAssetId");
        ResponseSchema(document, "/api/v1/media/{mediaAssetId}/complete", "post", 200, "ConfirmResponse");
        ResponseSchema(document, "/api/v1/media/{mediaAssetId}", "get", 200, "MediaAssetResponse");
        ComponentProperties(document, "PresignedUrlResponse").Should()
            .Contain(["mediaAssetId", "uploadUrl", "headers", "expiresAt"]);
        ComponentProperties(document, "ConfirmResponse").Should().Contain(["mediaAssetId", "status"]);
        ComponentProperties(document, "ProductResponse").Should()
            .Contain(["imageUrl", "imageUrlExpiresAt"]);
        RequestSchemaProperties(document, "/api/v1/products", "post").Should().Contain("mediaAssetId");
        ResponseSchema(document, "/api/v1/products", "post", 422, "ApiError");
        ResponseSchema(document, "/api/v1/products/{productId}", "patch", 422, "ApiError");
    }

    [Fact]
    public async Task LogoutDocumentsBearerResponses()
    {
        using var document = await LoadDocumentAsync();
        var responses = document.RootElement.GetProperty("paths")
            .GetProperty("/api/v1/auth/logout").GetProperty("post")
            .GetProperty("responses");

        responses.TryGetProperty("204", out _).Should().BeTrue();
        responses.TryGetProperty("401", out var unauthorized).Should().BeTrue();
        unauthorized.GetProperty("content").GetProperty("application/json").GetProperty("schema")
            .GetProperty("$ref").GetString().Should().Be("#/components/schemas/ApiError");
        responses.TryGetProperty("503", out var unavailable).Should().BeTrue();
        unavailable.GetProperty("content").GetProperty("application/json").GetProperty("schema")
            .GetProperty("$ref").GetString().Should().Be("#/components/schemas/ApiError");
    }
    [Fact]
    public async Task TheDocumentAdvertisesThePublicHttpsServer()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var servers = document.RootElement.GetProperty("servers");
        servers.GetArrayLength().Should().Be(1);
        servers[0].GetProperty("url").GetString().Should().Be("https://3.129.142.54.nip.io");
    }

    [Fact]
    public async Task ProductRoutesDocumentTheirRealResponsesAndSchemas()
    {
        var document = await LoadDocumentAsync();

        ResponseSchema(document, "/api/v1/products", "get", 200, "ProductListResponse");
        ResponseSchema(document, "/api/v1/products", "get", 400, "ApiError");
        ResponseSchema(document, "/api/v1/products", "get", 429, "ApiError");
        ResponseSchema(document, "/api/v1/products", "get", 503, "ApiError");

        ResponseSchema(document, "/api/v1/products/{productId}", "get", 200, "ProductResponse");
        ResponseSchema(document, "/api/v1/products/{productId}", "get", 404, "ApiError");

        ResponseSchema(document, "/api/v1/products", "post", 201, "ProductResponse");
        ResponseSchema(document, "/api/v1/products", "post", 401, "ApiError");
        ResponseSchema(document, "/api/v1/products", "post", 403, "ApiError");
        ResponseSchema(document, "/api/v1/products", "post", 404, "ApiError");

        ResponseSchema(document, "/api/v1/products/{productId}", "patch", 200, "ProductResponse");
        ResponseSchema(document, "/api/v1/products/{productId}", "patch", 409, "ApiError");
        ResponseSchema(document, "/api/v1/products/{productId}/availability", "patch", 200, "ProductResponse");

        ResponseSchema(document, "/api/v1/categories", "get", 200, "CategoryListResponse");
    }

    [Fact]
    public async Task OrderRoutesDocumentTheirRealResponsesAndSchemas()
    {
        var document = await LoadDocumentAsync();

        ResponseSchema(document, "/api/v1/orders", "post", 201, "OrderResponse");
        ResponseSchema(document, "/api/v1/orders", "post", 200, "OrderResponse");
        ResponseSchema(document, "/api/v1/orders", "post", 400, "ApiError");
        ResponseSchema(document, "/api/v1/orders", "post", 401, "ApiError");
        ResponseSchema(document, "/api/v1/orders", "post", 403, "ApiError");
        ResponseSchema(document, "/api/v1/orders", "post", 404, "ApiError");
        ResponseSchema(document, "/api/v1/orders", "post", 422, "ApiError");
        ResponseSchema(document, "/api/v1/orders", "post", 429, "ApiError");
        ResponseSchema(document, "/api/v1/orders", "post", 503, "ApiError");

        ResponseSchema(document, "/api/v1/orders/mine", "get", 200, "OrderListResponse");
        ResponseSchema(document, "/api/v1/orders/mine", "get", 400, "ApiError");
        ResponseSchema(document, "/api/v1/orders/mine", "get", 401, "ApiError");

        ResponseSchema(document, "/api/v1/orders/{orderId}", "get", 200, "OrderResponse");
        ResponseSchema(document, "/api/v1/orders/{orderId}", "get", 404, "ApiError");

        ResponseSchema(document, "/api/v1/orders/{orderId}/cancel", "post", 200, "OrderCancellationResponse");
        ResponseSchema(document, "/api/v1/orders/{orderId}/cancel", "post", 401, "ApiError");
        ResponseSchema(document, "/api/v1/orders/{orderId}/cancel", "post", 403, "ApiError");
        ResponseSchema(document, "/api/v1/orders/{orderId}/cancel", "post", 404, "ApiError");
        ResponseSchema(document, "/api/v1/orders/{orderId}/cancel", "post", 409, "ApiError");
        ResponseSchema(document, "/api/v1/orders/{orderId}/cancel", "post", 429, "ApiError");
        ResponseSchema(document, "/api/v1/orders/{orderId}/cancel", "post", 503, "ApiError");

        ResponseSchema(document, "/api/v1/orders/{orderId}/status", "patch", 200, "OrderStatusUpdateResponse");
        ResponseSchema(document, "/api/v1/orders/{orderId}/status", "patch", 400, "ApiError");
        ResponseSchema(document, "/api/v1/orders/{orderId}/status", "patch", 401, "ApiError");
        ResponseSchema(document, "/api/v1/orders/{orderId}/status", "patch", 403, "ApiError");
        ResponseSchema(document, "/api/v1/orders/{orderId}/status", "patch", 404, "ApiError");
        ResponseSchema(document, "/api/v1/orders/{orderId}/status", "patch", 409, "ApiError");
        ResponseSchema(document, "/api/v1/orders/{orderId}/status", "patch", 429, "ApiError");
        ResponseSchema(document, "/api/v1/orders/{orderId}/status", "patch", 503, "ApiError");

        ResponseSchema(document, "/api/v1/orders/{orderId}/delivery-token", "post", 201,
            "DeliveryTokenResponse");
        ResponseSchema(document, "/api/v1/orders/{orderId}/delivery-token", "post", 401, "ApiError");
        ResponseSchema(document, "/api/v1/orders/{orderId}/delivery-token", "post", 403, "ApiError");
        ResponseSchema(document, "/api/v1/orders/{orderId}/delivery-token", "post", 404, "ApiError");
        ResponseSchema(document, "/api/v1/orders/{orderId}/delivery-token", "post", 409, "ApiError");
        ResponseSchema(document, "/api/v1/orders/{orderId}/delivery-token", "post", 429, "ApiError");
        ResponseSchema(document, "/api/v1/orders/{orderId}/delivery-token", "post", 503, "ApiError");

        ResponseSchema(document, "/api/v1/orders/{orderId}/delivery-confirmations", "post", 200,
            "DeliveryConfirmationResponse");
        ResponseSchema(document, "/api/v1/orders/{orderId}/delivery-confirmations", "post", 400, "ApiError");
        ResponseSchema(document, "/api/v1/orders/{orderId}/delivery-confirmations", "post", 401, "ApiError");
        ResponseSchema(document, "/api/v1/orders/{orderId}/delivery-confirmations", "post", 403, "ApiError");
        ResponseSchema(document, "/api/v1/orders/{orderId}/delivery-confirmations", "post", 404, "ApiError");
        ResponseSchema(document, "/api/v1/orders/{orderId}/delivery-confirmations", "post", 409, "ApiError");
        ResponseSchema(document, "/api/v1/orders/{orderId}/delivery-confirmations", "post", 422, "ApiError");
        ResponseSchema(document, "/api/v1/orders/{orderId}/delivery-confirmations", "post", 429, "ApiError");
        ResponseSchema(document, "/api/v1/orders/{orderId}/delivery-confirmations", "post", 503, "ApiError");
    }

    [Fact]
    public async Task MobileRequestSchemasExposeStatusImageUrlAndNotes()
    {
        var document = await LoadDocumentAsync();

        var availability = RequestSchemaProperties(document, "/api/v1/products/{productId}/availability",
            "patch");
        availability.Should().Contain("status");
        availability.Should().NotContain("availability");

        RequestSchemaProperties(document, "/api/v1/products", "post").Should().Contain("imageUrl");
        RequestSchemaProperties(document, "/api/v1/orders", "post").Should().Contain("notes");
        ComponentProperties(document, "OrderResponse").Should().Contain("notes");
    }

    [Fact]
    public async Task SellerRoutesDocumentTheirRealResponsesAndSchemas()
    {
        var document = await LoadDocumentAsync();

        ResponseSchema(document, "/api/v1/sellers/activation-requests", "post", 201,
            "SellerOwnProfileResponse");
        ResponseSchema(document, "/api/v1/sellers/activation-requests", "post", 200,
            "SellerOwnProfileResponse");
        ResponseSchema(document, "/api/v1/sellers/activation-requests", "post", 400, "ApiError");
        ResponseSchema(document, "/api/v1/sellers/activation-requests", "post", 401, "ApiError");
        ResponseSchema(document, "/api/v1/sellers/activation-requests", "post", 403, "ApiError");
        ResponseSchema(document, "/api/v1/sellers/activation-requests", "post", 404, "ApiError");
        ResponseSchema(document, "/api/v1/sellers/activation-requests", "post", 409, "ApiError");

        ResponseSchema(document, "/api/v1/sellers/me", "get", 200, "SellerOwnProfileResponse");
        ResponseSchema(document, "/api/v1/sellers/me", "get", 401, "ApiError");
        ResponseSchema(document, "/api/v1/sellers/me", "get", 404, "ApiError");

        ResponseSchema(document, "/api/v1/sellers/me", "patch", 200, "SellerOwnProfileResponse");
        ResponseSchema(document, "/api/v1/sellers/{sellerId}", "get", 200, "SellerProfileResponse");

        ResponseSchema(document, "/api/v1/sellers/me/orders", "get", 200, "OrderListResponse");
        ResponseSchema(document, "/api/v1/sellers/me/orders", "get", 400, "ApiError");
        ResponseSchema(document, "/api/v1/sellers/me/orders", "get", 401, "ApiError");
        ResponseSchema(document, "/api/v1/sellers/me/orders", "get", 403, "ApiError");
        ResponseSchema(document, "/api/v1/sellers/me/orders", "get", 429, "ApiError");
        ResponseSchema(document, "/api/v1/sellers/me/orders", "get", 503, "ApiError");
    }

    [Fact]
    public async Task SellerSchemasExposeStoreNameDescriptionAndPhone()
    {
        var document = await LoadDocumentAsync();

        var activation = RequestSchemaProperties(document, "/api/v1/sellers/activation-requests", "post");
        activation.Should().Contain("storeName").And.Contain("description").And.Contain("phone");
        activation.Should().NotContain("businessName").And.NotContain("pickupPoint")
            .And.NotContain("unknownFields");

        var ownProfile = ComponentProperties(document, "SellerOwnProfileResponse");
        ownProfile.Should().Contain("storeName").And.Contain("description").And.Contain("phone");
        ownProfile.Should().Contain("status").And.Contain("isActive").And.Contain("roles");
        ownProfile.Should().NotContain("businessName").And.NotContain("pickupPoint");
    }

    [Fact]
    public async Task PaginatedSchemasExposeTotalCountInsteadOfTotal()
    {
        var document = await LoadDocumentAsync();

        foreach (var schemaName in new[] { "ProductListResponse", "CategoryListResponse",
            "OrderListResponse", "AdminUsersResponse" })
        {
            var properties = ComponentProperties(document, schemaName);
            properties.Should().Contain("totalCount");
            properties.Should().NotContain("total");
        }
    }

    [Fact]
    public async Task OrderSchemasExposeTheBuyerAndSellerRelations()
    {
        var document = await LoadDocumentAsync();

        var order = ComponentProperties(document, "OrderResponse");
        order.Should().Contain("buyerId").And.Contain("sellerId").And.Contain("productName")
            .And.Contain("unitPrice").And.Contain("quantity").And.Contain("totalAmount")
            .And.Contain("status").And.Contain("notes").And.Contain("createdAt");
        order.Should().NotContain("buyerUserId").And.NotContain("total");

        var summary = ComponentProperties(document, "OrderSummaryResponse");
        summary.Should().Contain("buyerId").And.Contain("sellerId").And.Contain("productName")
            .And.Contain("unitPrice").And.Contain("quantity").And.Contain("totalAmount")
            .And.Contain("status").And.Contain("notes").And.Contain("createdAt");
        summary.Should().NotContain("buyerUserId").And.NotContain("total");
    }

    private static async Task<JsonDocument> LoadDocumentAsync()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static IReadOnlyList<string> RequestSchemaProperties(JsonDocument document, string path,
        string method)
    {
        var schema = document.RootElement.GetProperty("paths").GetProperty(path).GetProperty(method)
            .GetProperty("requestBody").GetProperty("content").GetProperty("application/json")
            .GetProperty("schema");
        return SchemaProperties(document, schema);
    }

    private static IReadOnlyList<string> ComponentProperties(JsonDocument document, string schemaName)
    {
        var schema = document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty(schemaName);
        return SchemaProperties(document, schema);
    }

    private static IReadOnlyList<string> SchemaProperties(JsonDocument document, JsonElement schema)
    {
        if (schema.TryGetProperty("$ref", out var reference))
        {
            var schemaName = reference.GetString()!.Split('/')[^1];
            schema = document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty(schemaName);
        }

        return schema.GetProperty("properties").EnumerateObject().Select(property => property.Name).ToList();
    }

    private static void ResponseSchema(JsonDocument document, string path, string method, int status,
        string schema)
    {
        var responses = document.RootElement.GetProperty("paths").GetProperty(path).GetProperty(method)
            .GetProperty("responses");
        responses.TryGetProperty(status.ToString(), out var response).Should().BeTrue(
            because: $"{method.ToUpperInvariant()} {path} must document the {status} response");
        var reference = response.GetProperty("content").GetProperty("application/json").GetProperty("schema")
            .GetProperty("$ref").GetString();
        reference.Should().Be($"#/components/schemas/{schema}");
    }
}
