using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using UTTTGO.Api.Modules.Identity.Application;

namespace UTTTGO.Api.Modules.Identity.Infrastructure;

public sealed class SupabaseAuthClient(HttpClient client, IOptions<SupabaseOptions> settings,
    IHttpContextAccessor context, IValidatedJwtSubjectReader subjects,
    ILogger<SupabaseAuthClient> logger) : ISupabaseRegistration,
    ISupabaseLogin, ISupabaseSessionIdentity
{
    // Never log an arbitrary provider-supplied string: it could contain credentials.
    private static readonly HashSet<string> SafeErrorCodes = new(StringComparer.Ordinal)
    {
        "bad_json", "bad_jwt", "email_exists", "email_not_confirmed", "invalid_credentials",
        "invalid_email", "invalid_grant", "invalid_jwt", "no_authorization", "not_admin",
        "otp_expired", "over_email_send_rate_limit", "over_request_rate_limit", "phone_exists",
        "provider_disabled", "refresh_token_already_used", "refresh_token_not_found",
        "request_timeout", "service_unavailable", "session_expired", "session_not_found",
        "signup_disabled", "unexpected_failure", "upstream_failure", "user_already_exists",
        "user_banned", "user_not_found", "validation_failed", "weak_password"
    };

    private Uri Endpoint(string path)
    {
        var options = settings.Value;
        if (string.IsNullOrWhiteSpace(options.Url) || string.IsNullOrWhiteSpace(options.AnonKey))
        {
            logger.LogWarning("Supabase Auth URL or anon key is missing.");
            throw new ExternalDependencyUnavailableException();
        }
        if (!Uri.TryCreate(options.Url, UriKind.Absolute, out var url) ||
            url.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(url.Query) ||
            !string.IsNullOrEmpty(url.Fragment))
            throw new ExternalDependencyUnavailableException();
        return new Uri(url.ToString().TrimEnd('/') + "/auth/v1/" + path);
    }

    private HttpRequestMessage Request(HttpMethod method, string path, string? bearer = null)
    {
        var request = new HttpRequestMessage(method, Endpoint(path));
        request.Headers.TryAddWithoutValidation("apikey", settings.Value.AnonKey);
        if (bearer is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        return request;
    }

    public async Task<SupabaseSignUpResult> SignUpAsync(string email, string password,
        CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Post, "signup");
        request.Content = JsonContent.Create(new { email, password });
        using var response = await client.SendAsync(request, cancellationToken);
        string? errorCode = null;
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity)
        {
            errorCode = await ReadErrorCodeAsync(response, cancellationToken);
            if (errorCode is "user_already_exists" or "email_exists")
                return new(true, null, null);
        }
        if (!response.IsSuccessStatusCode)
            await ThrowUnavailableAsync(response, cancellationToken, errorCode,
                response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity);
        using var body = await ReadJsonAsync(response, cancellationToken);
        var user = body.RootElement.TryGetProperty("user", out var nested) ? nested : body.RootElement;
        if (user.TryGetProperty("identities", out var identities) &&
            identities.ValueKind == JsonValueKind.Array && identities.GetArrayLength() == 0)
            return new(true, null, null);
        return new(false, Field(user, "id"), Field(user, "email"));
    }

    public async Task<SupabaseLoginResult> AuthenticateAndCheckConfirmationAsync(string email, string password,
        CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Post, "token?grant_type=password");
        request.Content = JsonContent.Create(new { email, password });
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or
            HttpStatusCode.UnprocessableEntity)
            return new(false, false, null, null, null, 0);
        if (!response.IsSuccessStatusCode) await ThrowUnavailableAsync(response, cancellationToken);
        using var body = await ReadJsonAsync(response, cancellationToken);
        var root = body.RootElement;
        if (!root.TryGetProperty("user", out var user) || user.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("expires_in", out var expiry) || !expiry.TryGetInt64(out var expiresIn))
            throw new ExternalDependencyUnavailableException();
        return new(true, Confirmed(user), Field(user, "id"), Field(user, "email"),
            Field(root, "access_token"), expiresIn);
    }

    public async Task<ConfirmedSessionIdentity?> FindBySubjectAsync(string subject,
        CancellationToken cancellationToken)
    {
        var authorization = context.HttpContext?.Request.Headers.Authorization.ToString();
        if (authorization is null || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;
        var token = authorization[7..].Trim();
        if (!string.Equals(await subjects.ReadValidatedSubjectAsync(token, cancellationToken), subject,
                StringComparison.Ordinal)) return null;

        using var request = Request(HttpMethod.Get, "user", token);
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) return null;
        if (!response.IsSuccessStatusCode) await ThrowUnavailableAsync(response, cancellationToken);
        using var body = await ReadJsonAsync(response, cancellationToken);
        var user = body.RootElement;
        var id = Field(user, "id");
        var email = Field(user, "email");
        return id is not null && email is not null && string.Equals(id, subject, StringComparison.Ordinal)
            ? new(id, email, Confirmed(user)) : null;
    }

    private static bool Confirmed(JsonElement user) =>
        Field(user, "email_confirmed_at") is not null || Field(user, "confirmed_at") is not null;

    private static string? Field(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response,
        CancellationToken cancellationToken) =>
        await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

    private async Task ThrowUnavailableAsync(HttpResponseMessage response,
        CancellationToken cancellationToken, string? errorCode = null, bool errorAlreadyRead = false)
    {
        if (!errorAlreadyRead) errorCode = await ReadErrorCodeAsync(response, cancellationToken);
        logger.LogWarning("Supabase Auth returned HTTP {StatusCode}; error code {ErrorCode}.",
            (int)response.StatusCode, SafeErrorCode(errorCode));
        throw new ExternalDependencyUnavailableException();
    }

    private static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            using var error = await ReadJsonAsync(response, cancellationToken);
            return Field(error.RootElement, "error_code") ?? Field(error.RootElement, "code");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? SafeErrorCode(string? code) =>
        code is not null && SafeErrorCodes.Contains(code) ? code : null;
}
