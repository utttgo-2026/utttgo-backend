using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace UTTTGO.Api.Shared.OpenApi;

// The public contract of the API is always served over HTTPS on the fixed public
// hostname of the instance: the mobile client must never build URLs from the
// scheme it used to reach the API. The value comes from OpenApi:PublicBaseUrl
// (appsettings.json or the OpenApi__PublicBaseUrl environment variable).
public sealed class PublicApiServerTransformer(IConfiguration configuration) : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var baseUrl = configuration["OpenApi:PublicBaseUrl"];
        if (!string.IsNullOrWhiteSpace(baseUrl))
            document.Servers = [new OpenApiServer { Url = baseUrl.Trim().TrimEnd('/') }];

        if (document.Paths.TryGetValue("/api/v1/products/{productId}", out var productPath) &&
            productPath is OpenApiPathItem openApiProductPath &&
            openApiProductPath.Operations is { } operations &&
            operations.TryGetValue(HttpMethod.Patch, out var patch) &&
            patch.RequestBody is OpenApiRequestBody requestBody)
        {
            var schema = new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                AdditionalPropertiesAllowed = false,
                Properties = new Dictionary<string, IOpenApiSchema>
                {
                    ["name"] = new OpenApiSchema { Type = JsonSchemaType.String },
                    ["description"] = new OpenApiSchema { Type = JsonSchemaType.String },
                    ["ingredients"] = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Array,
                        Items = new OpenApiSchema { Type = JsonSchemaType.String }
                    },
                    ["price"] = new OpenApiSchema { Type = JsonSchemaType.Number },
                    ["stock"] = new OpenApiSchema { Type = JsonSchemaType.Integer },
                    ["mediaAssetId"] = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String | JsonSchemaType.Null,
                        Description = "Approved Media asset id; null explicitly removes the image."
                    }
                }
            };
            foreach (var content in requestBody.Content is { } contentMap ? contentMap.Values : [])
                if (content is not null)
                    content.Schema = schema;
        }

        MarkAccountDeletionHeadersRequired(document);
        return Task.CompletedTask;
    }

    private static void MarkAccountDeletionHeadersRequired(OpenApiDocument document)
    {
        if (!document.Paths.TryGetValue("/api/v1/users/me", out var deletePath) ||
            !document.Paths.TryGetValue("/api/v1/users/me/deletion/{operationId}", out var statusPath))
            return;

        if (deletePath.Operations is not { } deleteOperations || statusPath.Operations is not { } statusOperations)
            return;

        foreach (var operation in deleteOperations.Values.Concat(statusOperations.Values))
        {
            if (operation is null) continue;
            foreach (var parameter in operation.Parameters ?? [])
            {
                if (parameter is not OpenApiParameter openApiParameter ||
                    openApiParameter.In != ParameterLocation.Header ||
                    openApiParameter.Name is not ("X-Account-Deletion-Id" or "X-Account-Deletion-Proof"))
                    continue;

                openApiParameter.Required = true;
            }
        }
    }
}
