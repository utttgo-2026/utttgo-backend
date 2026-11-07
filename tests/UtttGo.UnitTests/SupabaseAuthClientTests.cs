using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Identity.Infrastructure;
using Xunit;

namespace UtttGo.UnitTests;

public sealed class SupabaseAuthClientTests
{
    [Fact]
    public async Task SignupSendsCredentialsOnlyToConfiguredEndpoint()
    {
        var (adapter, handler) = Create(HttpStatusCode.OK,
            """{"id":"sub-1","email":"a@uttt.edu.mx","identities":[{"id":"one"}]}""");
        var result = await adapter.SignUpAsync("a@uttt.edu.mx", "password", default);
        result.Should().Be(new SupabaseSignUpResult(false, "sub-1", "a@uttt.edu.mx"));
        handler.Path.Should().Be("/auth/v1/signup");
        handler.Method.Should().Be(HttpMethod.Post);
        handler.ApiKey.Should().Be("test-key");
        handler.Body.Should().Contain("password");
    }

    [Fact]
    public async Task SignupObfuscatedExistingAccountDoesNotPersistAnotherProfile()
    {
        var (adapter, _) = Create(HttpStatusCode.OK,
            """{"id":"masked","email":"a@uttt.edu.mx","identities":[]}""");
        (await adapter.SignUpAsync("a@uttt.edu.mx", "password", default)).AlreadyExists.Should().BeTrue();
    }

    [Fact]
    public async Task LoginReadsConfirmedUserAndAccessToken()
    {
        var (adapter, handler) = Create(HttpStatusCode.OK,
            """{"access_token":"token","expires_in":3600,"user":{"id":"sub-1","email":"a@uttt.edu.mx","email_confirmed_at":"2026-11-06T00:00:00Z"}}""");
        var result = await adapter.AuthenticateAndCheckConfirmationAsync("a@uttt.edu.mx", "password", default);
        result.Should().Be(new SupabaseLoginResult(true, true, "sub-1", "a@uttt.edu.mx", "token", 3600));
        handler.Path.Should().Be("/auth/v1/token?grant_type=password");
    }

    [Fact]
    public async Task SessionUsesValidatedBearerAndRejectsDifferentSubject()
    {
        var (adapter, handler) = Create(HttpStatusCode.OK,
            """{"id":"sub-1","email":"a@uttt.edu.mx","email_confirmed_at":"2026-11-06T00:00:00Z"}""",
            "sub-1");
        (await adapter.FindBySubjectAsync("sub-1", default)).Should()
            .Be(new ConfirmedSessionIdentity("sub-1", "a@uttt.edu.mx", true));
        handler.Bearer.Should().Be("token");
        (await adapter.FindBySubjectAsync("other", default)).Should().BeNull();
        handler.Calls.Should().Be(1);
    }

    [Theory]
    [InlineData("error_code")]
    [InlineData("code")]
    public async Task SignupUnavailableLogsOnlyHttpStatusAndErrorCode(string field)
    {
        var logger = new RecordingLogger();
        var (adapter, _) = Create(HttpStatusCode.ServiceUnavailable,
            $$"""{"{{field}}":"service_unavailable","email":"private@uttt.edu.mx","access_token":"private-token","password":"private-password","apikey":"private-key"}""",
            logger: logger);

        await FluentActions.Invoking(() => adapter.SignUpAsync("private@uttt.edu.mx", "private-password", default))
            .Should().ThrowAsync<ExternalDependencyUnavailableException>();

        logger.Warnings.Should().ContainSingle().Which.Should().Contain("503").And.Contain("service_unavailable")
            .And.NotContain("private@uttt.edu.mx").And.NotContain("private-token")
            .And.NotContain("private-password").And.NotContain("private-key");
    }

    [Fact]
    public async Task UnexpectedSignupErrorLogsStatusAndSafeCode()
    {
        var logger = new RecordingLogger();
        var (adapter, _) = Create(HttpStatusCode.BadRequest, """{"code":"unexpected_failure"}""",
            logger: logger);

        await FluentActions.Invoking(() => adapter.SignUpAsync("a@uttt.edu.mx", "password", default))
            .Should().ThrowAsync<ExternalDependencyUnavailableException>();
        logger.Warnings.Should().ContainSingle().Which.Should().Contain("400").And.Contain("unexpected_failure");
    }

    [Fact]
    public async Task LoginUnavailableDoesNotLogUnsafeCodeOrResponseBody()
    {
        var logger = new RecordingLogger();
        var (adapter, _) = Create(HttpStatusCode.InternalServerError,
            """{"error_code":"private@uttt.edu.mx","access_token":"private-token"}""", logger: logger);

        await FluentActions.Invoking(() => adapter.AuthenticateAndCheckConfirmationAsync(
                "private@uttt.edu.mx", "private-password", default))
            .Should().ThrowAsync<ExternalDependencyUnavailableException>();
        logger.Warnings.Should().ContainSingle().Which.Should().Contain("500")
            .And.NotContain("private@uttt.edu.mx").And.NotContain("private-token");
    }

    [Fact]
    public async Task ArbitraryErrorCodeCannotBeLoggedAsACredential()
    {
        var logger = new RecordingLogger();
        var (adapter, _) = Create(HttpStatusCode.InternalServerError,
            """{"code":"private_password"}""", logger: logger);

        await FluentActions.Invoking(() => adapter.SignUpAsync("a@uttt.edu.mx", "password", default))
            .Should().ThrowAsync<ExternalDependencyUnavailableException>();
        logger.Warnings.Should().ContainSingle().Which.Should().Contain("500")
            .And.NotContain("private_password");
    }

    [Fact]
    public async Task SessionUnavailableLogsStatusAndCode()
    {
        var logger = new RecordingLogger();
        var (adapter, _) = Create(HttpStatusCode.BadGateway, """{"code":"upstream_failure"}""",
            "sub-1", logger);

        await FluentActions.Invoking(() => adapter.FindBySubjectAsync("sub-1", default))
            .Should().ThrowAsync<ExternalDependencyUnavailableException>();
        logger.Warnings.Should().ContainSingle().Which.Should().Contain("502").And.Contain("upstream_failure");
    }

    [Theory]
    [InlineData(null, "private-key")]
    [InlineData("https://example.supabase.co", null)]
    public async Task MissingConfigurationLogsWarningWithoutValues(string? url, string? key)
    {
        var logger = new RecordingLogger();
        var (adapter, handler) = Create(HttpStatusCode.OK, "{}", logger: logger, url: url, key: key);

        await FluentActions.Invoking(() => adapter.SignUpAsync("private@uttt.edu.mx", "private-password", default))
            .Should().ThrowAsync<ExternalDependencyUnavailableException>();
        handler.Calls.Should().Be(0);
        logger.Warnings.Should().ContainSingle().Which.Should().Contain("missing")
            .And.NotContain("example.supabase.co").And.NotContain("private@uttt.edu.mx")
            .And.NotContain("private-key");
    }

    private static (SupabaseAuthClient Adapter, ResponseHandler Handler) Create(HttpStatusCode status,
        string json, string? validatedSubject = null, ILogger<SupabaseAuthClient>? logger = null,
        string? url = "https://example.supabase.co", string? key = "test-key")
    {
        var handler = new ResponseHandler(status, json);
        var context = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        context.HttpContext.Request.Headers.Authorization = "Bearer token";
        return (new SupabaseAuthClient(new HttpClient(handler),
            Options.Create(new SupabaseOptions { Url = url, AnonKey = key }),
            context, new SubjectReader(validatedSubject), logger ?? NullLogger<SupabaseAuthClient>.Instance), handler);
    }

    private sealed class SubjectReader(string? subject) : IValidatedJwtSubjectReader
    {
        public Task<string?> ReadValidatedSubjectAsync(string token, CancellationToken cancellationToken) =>
            Task.FromResult(subject);
    }

    private sealed class RecordingLogger : ILogger<SupabaseAuthClient>
    {
        public List<string> Warnings { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning) Warnings.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    private sealed class ResponseHandler(HttpStatusCode status, string json) : HttpMessageHandler
    {
        public string? Path { get; private set; }
        public HttpMethod? Method { get; private set; }
        public string? ApiKey { get; private set; }
        public string? Body { get; private set; }
        public string? Bearer { get; private set; }
        public int Calls { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            Path = request.RequestUri?.PathAndQuery;
            Method = request.Method;
            ApiKey = request.Headers.GetValues("apikey").Single();
            Bearer = request.Headers.Authorization?.Parameter;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }
    }
}
