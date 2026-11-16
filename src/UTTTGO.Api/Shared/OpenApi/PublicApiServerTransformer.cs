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

        return Task.CompletedTask;
    }
}
