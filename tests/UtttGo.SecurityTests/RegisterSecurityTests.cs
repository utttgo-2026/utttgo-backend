using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UTTTGO.Api.Modules.Identity.Application;
using Xunit;

namespace UtttGo.SecurityTests;

public sealed class RegisterSecurityTests
{
    private const string Route = "/api/v1/auth/register";
    private const string Password = "private-password-otp-jwt-sentinel";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublicAcceptanceHasNoSensitiveInformation(bool existing)
    {
        using var factory = CreateFactory(existing);
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(Route,
            new { email = "a@uttt.edu.mx", password = Password, displayName = "Private Name" });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        response.Headers.Location.Should().BeNull();
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("a@uttt.edu.mx").And.NotContain("Private Name")
            .And.NotContain(Password).And.NotContain("subject-secret")
            .And.NotContain("Buyer").And.NotContain("PendingEmailVerification")
            .And.NotContain("accessToken").And.NotContain("refreshToken")
            .And.NotContain("otp").And.NotContain("userId");
    }

    [Fact]
    public async Task NewAndExistingSameCodeBodyAndContentTypeNotProofOfTimingResistance()
    {
        using var first = CreateFactory(false);
        using var second = CreateFactory(true);
        using var firstClient = first.CreateClient();
        using var secondClient = second.CreateClient();
        var request = new { email = "a@uttt.edu.mx", password = Password, displayName = "Private Name" };

        using var newUser = await firstClient.PostAsJsonAsync(Route, request);
        using var existing = await secondClient.PostAsJsonAsync(Route, request);

        existing.StatusCode.Should().Be(newUser.StatusCode);
        existing.Content.Headers.ContentType?.MediaType.Should().Be(newUser.Content.Headers.ContentType?.MediaType);
        (await existing.Content.ReadAsStringAsync()).Should().Be(await newUser.Content.ReadAsStringAsync());
        // No duration assertion: remote provider and persistence paths can still be distinguishable.
    }

    [Theory]
    [InlineData("roles")]
    [InlineData("role")]
    [InlineData("status")]
    [InlineData("accountStatus")]
    [InlineData("emailVerified")]
    [InlineData("isAdmin")]
    public async Task PrivilegedJsonFieldsCannotBeAssigned(string field)
    {
        using var factory = CreateFactory(false);
        using var client = factory.CreateClient();
        using var content = new System.Net.Http.StringContent(
            $"{{\"email\":\"a@uttt.edu.mx\",\"password\":\"{Password}\",\"displayName\":\"Private Name\",\"{field}\":\"Admin\"}}",
            System.Text.Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(Route, content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("Admin").And.NotContain(Password).And.Contain("correlationId");
    }

    private static WebApplicationFactory<Program> CreateFactory(bool existing) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAuthRateLimitGate>();
            services.RemoveAll<ISupabaseRegistration>();
            services.RemoveAll<IUserRegistrationRepository>();
            services.AddSingleton<IAuthRateLimitGate>(new AllowedGate());
            services.AddSingleton<ISupabaseRegistration>(new AuthDouble(existing));
            services.AddSingleton<IUserRegistrationRepository>(new StoreDouble());
        }));

    private sealed class AllowedGate : IAuthRateLimitGate
    {
        public Task<bool> IsAllowedAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class AuthDouble(bool existing) : ISupabaseRegistration
    {
        public Task<SupabaseSignUpResult> SignUpAsync(string email, string password,
            CancellationToken cancellationToken) => Task.FromResult(existing
                ? new SupabaseSignUpResult(true, null, null)
                : new SupabaseSignUpResult(false, "subject-secret", email));
    }

    private sealed class StoreDouble : IUserRegistrationRepository
    {
        public Task EnsureAvailableAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SavePendingBuyerAsync(PendingBuyerProfile profile, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
