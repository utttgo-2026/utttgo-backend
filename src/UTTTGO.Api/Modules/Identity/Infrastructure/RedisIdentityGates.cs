using Microsoft.AspNetCore.Mvc.Filters;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Identity.Contracts;
using UTTTGO.Api.Shared.Infrastructure;

namespace UTTTGO.Api.Modules.Identity.Infrastructure;

// Model binding has already validated the action's arguments when this filter runs.
public sealed class LoginRateLimitPartitionFilter : IAsyncActionFilter
{
    internal const string EmailKey = "rate-limit-login-email";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.HttpContext.Request.Path.Equals("/api/v1/auth/login", StringComparison.OrdinalIgnoreCase) &&
            context.ActionArguments.TryGetValue("request", out var value) && value is LoginRequest request &&
            request.Email is not null)
            context.HttpContext.Items[EmailKey] = request.Email.Trim().ToLowerInvariant();

        await next();
    }
}

public sealed class RedisAuthRateLimitGate(RedisRateLimiter limiter, IHttpContextAccessor httpContext)
    : IAuthRateLimitGate
{
    public Task<bool> IsAllowedAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var context = httpContext.HttpContext ?? throw new InvalidOperationException("Request unavailable.");
        var ip = limiter.ClientIp;
        if (context.Request.Path.Equals("/api/v1/auth/register", StringComparison.OrdinalIgnoreCase))
            return limiter.IsAllowedAsync(("auth-register", ip));
        if (context.Request.Path.Equals("/api/v1/auth/login", StringComparison.OrdinalIgnoreCase) &&
            context.Items[LoginRateLimitPartitionFilter.EmailKey] is string email && email.Length > 0)
            return limiter.IsAllowedAsync(("auth-login-ip", ip), ("auth-login-email", email));

        throw new InvalidOperationException("Authentication rate limit partition unavailable.");
    }
}
