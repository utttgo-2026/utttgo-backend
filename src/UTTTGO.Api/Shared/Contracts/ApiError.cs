using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;
using System.Text;

namespace UTTTGO.Api.Shared.Contracts;

public sealed record ApiError(string CorrelationId, int Status, string Code, string Message)
{
    public static ApiError Create(HttpContext context, int status, string code, string message) =>
        new(PublicCorrelationId(context.TraceIdentifier), status, code, message);

    private static string PublicCorrelationId(string traceIdentifier)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(traceIdentifier));
        var letters = new char[hash.Length];
        for (var index = 0; index < hash.Length; index++)
            letters[index] = (char)('a' + hash[index] % 26);
        return $"req-{new string(letters)}";
    }
}
