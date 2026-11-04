using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Identity.Contracts;
using UTTTGO.Api.Modules.Identity.Validators;

namespace UTTTGO.Api.Modules.Identity.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(
    IRegistrationService registration,
    IValidator<RegisterRequest> validator,
    ILoginService login,
    IValidator<LoginRequest> loginValidator,
    ISessionService session) : ControllerBase
{
    private const string AcceptedMessage = "Si el registro puede procesarse, recibirás instrucciones para verificar tu correo.";

    [HttpPost("register")]
    [RequestSizeLimit(8 * 1024)] // Candidate limit for this small JSON body; not an approved NFR threshold.
    [TypeFilter(typeof(RegistrationRequestSizeFilter))]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return BadRequest(ApiError.Create(HttpContext, 400, "VALIDATION_ERROR", "Solicitud inválida."));

        var email = request.Email!.Trim().ToLowerInvariant();
        if (!RegisterRequestValidator.HasInstitutionalDomain(email))
            return UnprocessableEntity(ApiError.Create(HttpContext, 422, "INSTITUTIONAL_EMAIL_REQUIRED",
                "Correo institucional requerido."));

        var result = await registration.RegisterAsync(email, request.Password!, request.DisplayName!.Trim(),
            cancellationToken);
        return result switch
        {
            RegistrationOutcome.Accepted => Accepted(new { message = AcceptedMessage }),
            RegistrationOutcome.RateLimited => StatusCode(429,
                ApiError.Create(HttpContext, 429, "RATE_LIMITED", "Intenta de nuevo más tarde.")),
            _ => StatusCode(503,
                ApiError.Create(HttpContext, 503, "AUTH_UNAVAILABLE", "Servicio temporalmente no disponible."))
        };
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var validation = await loginValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return BadRequest(ApiError.Create(HttpContext, 400, "VALIDATION_ERROR", "Solicitud inválida."));

        var result = await login.LoginAsync(request.Email!.Trim().ToLowerInvariant(), request.Password!,
            cancellationToken);
        return result.Outcome switch
        {
            LoginOutcome.Success when result.Response is not null => Ok(result.Response),
            LoginOutcome.InvalidCredentials => Unauthorized(ApiError.Create(HttpContext, 401,
                "INVALID_CREDENTIALS", "No se pudo iniciar sesión con esas credenciales.")),
            LoginOutcome.RateLimited => StatusCode(429,
                ApiError.Create(HttpContext, 429, "RATE_LIMITED", "Intenta de nuevo más tarde.")),
            _ => StatusCode(503,
                ApiError.Create(HttpContext, 503, "AUTH_UNAVAILABLE", "Servicio temporalmente no disponible."))
        };
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var token = BearerTokenReader.Read(Request);
        if (token is null)
            return Unauthorized(ApiError.Create(HttpContext, 401, "UNAUTHORIZED", "Sesión no autorizada."));

        var result = await session.GetSessionAsync(token, cancellationToken);
        return result.Outcome switch
        {
            SessionOutcome.Success when result.Response is not null => Ok(result.Response),
            SessionOutcome.ProfileMissing => NotFound(ApiError.Create(HttpContext, 404,
                "PROFILE_NOT_FOUND", "Perfil no disponible.")),
            SessionOutcome.Unauthorized => Unauthorized(ApiError.Create(HttpContext, 401,
                "UNAUTHORIZED", "Sesión no autorizada.")),
            _ => StatusCode(503, ApiError.Create(HttpContext, 503,
                "AUTH_UNAVAILABLE", "Servicio temporalmente no disponible."))
        };
    }
}
