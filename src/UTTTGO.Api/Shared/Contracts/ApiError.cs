using Microsoft.AspNetCore.Http;

namespace UTTTGO.Api.Shared.Contracts;

public sealed record ApiError(string CorrelationId, int Status, string Code, string Message)
{
    public static ApiError Create(HttpContext context, int status, string code, string message) =>
        new(context.TraceIdentifier, status, code, message);
}
