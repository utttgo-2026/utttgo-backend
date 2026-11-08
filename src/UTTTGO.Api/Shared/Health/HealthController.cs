using Microsoft.AspNetCore.Mvc;
using UTTTGO.Api.Shared.Contracts;

namespace UTTTGO.Api.Shared.Health;

[ApiController]
[Route("health")]
public sealed class HealthController(IHealthService health, IHealthRateLimitGate rateLimit) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        try
        {
            if (!await rateLimit.IsAllowedAsync(cancellationToken))
            {
                // The distributed limiter publishes the remaining window; keep it when present.
                if (!Response.Headers.ContainsKey("Retry-After"))
                    Response.Headers.RetryAfter = "60";
                return StatusCode(429, ApiError.Create(HttpContext, 429, "RATE_LIMITED",
                    "Intenta de nuevo más tarde."));
            }
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Distributed rate limiting is fail-closed: without Redis there is no evidence
            // of availability, so the probe answers 503 instead of an unhandled 500.
            return StatusCode(503, ApiError.Create(HttpContext, 503, "DEPENDENCY_UNAVAILABLE",
                "Servicio temporalmente no disponible."));
        }

        var report = await health.GetAsync(cancellationToken);
        if (!report.IsHealthy)
            return StatusCode(503, ApiError.Create(HttpContext, 503, "DEPENDENCY_UNAVAILABLE",
                "Servicio temporalmente no disponible."));

        var checks = report.Dependencies.ToDictionary(
            dependency => dependency.Name, dependency => dependency.Status);
        return Ok(new HealthResponse("Healthy", checks));
    }
}
