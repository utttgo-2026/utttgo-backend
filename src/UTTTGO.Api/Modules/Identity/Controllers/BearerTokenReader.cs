using Microsoft.AspNetCore.Http;

namespace UTTTGO.Api.Modules.Identity.Controllers;

internal static class BearerTokenReader
{
    public static string? Read(HttpRequest request)
    {
        var authorization = request.Headers.Authorization;
        if (authorization.Count != 1 || authorization[0] is not string header ||
            !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ||
            header.Length <= 7 || header[7..].Trim() != header[7..] ||
            header[7..].Any(char.IsWhiteSpace))
            return null;

        return header[7..];
    }
}
