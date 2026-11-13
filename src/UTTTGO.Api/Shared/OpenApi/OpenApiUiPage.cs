namespace UTTTGO.Api.Shared.OpenApi;

public static class OpenApiUiPage
{
    public const string Html = """
        <!doctype html>
        <html lang="es">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>UTTT-GO API — OpenAPI</title>
          <link rel="stylesheet" href="https://unpkg.com/swagger-ui-dist@5/swagger-ui.css">
          <style>
            body { margin: 0; background: #fafafa; }
          </style>
        </head>
        <body>
          <div id="swagger-ui"></div>
          <script src="https://unpkg.com/swagger-ui-dist@5/swagger-ui-bundle.js"></script>
          <script>
            window.onload = function () {
              window.ui = SwaggerUIBundle({
                url: '/openapi/v1.json',
                dom_id: '#swagger-ui',
                deepLinking: true,
                tryItOutEnabled: false,
                docExpansion: 'list'
              });
            };
          </script>
        </body>
        </html>
        """;
}
