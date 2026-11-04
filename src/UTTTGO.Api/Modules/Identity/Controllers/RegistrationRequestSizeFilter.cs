using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using UTTTGO.Api.Modules.Identity.Contracts;

namespace UTTTGO.Api.Modules.Identity.Controllers;

public sealed class RegistrationRequestSizeFilter : IAsyncResourceFilter
{
    private const int MaxBodyBytes = 8 * 1024; // Candidate local limit, not a project-wide NFR.

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if (request.ContentLength > MaxBodyBytes)
        {
            Reject(context);
            return;
        }

        request.EnableBuffering(bufferThreshold: MaxBodyBytes, bufferLimit: MaxBodyBytes);
        try
        {
            await request.Body.CopyToAsync(Stream.Null, context.HttpContext.RequestAborted);
            request.Body.Position = 0;
        }
        catch (IOException)
        {
            Reject(context);
            return;
        }

        await next();
    }

    private static void Reject(ResourceExecutingContext context) => context.Result = new ObjectResult(
        ApiError.Create(context.HttpContext, 413, "REQUEST_TOO_LARGE", "Solicitud demasiado grande."))
    {
        StatusCode = 413
    };
}
