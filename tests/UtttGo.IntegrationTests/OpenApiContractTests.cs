using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace UtttGo.IntegrationTests;

public sealed class OpenApiContractTests
{
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
