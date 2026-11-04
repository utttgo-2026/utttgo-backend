using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UTTTGO.Api.Modules.Identity.Application;
using Xunit;

namespace UtttGo.SecurityTests;

public sealed class UpdateOwnProfileSecurityTests
{
    [Theory]
    [InlineData(null, true, true, "Active", "sub-1", false)]
    [InlineData("sub-1", false, true, "Active", "sub-1", false)]
    [InlineData("sub-1", true, false, "Active", "sub-1", false)]
    [InlineData("sub-1", true, true, "Suspended", "sub-1", false)]
    [InlineData("sub-1", true, true, "Active", "other-sub", false)]
    [InlineData("sub-1", true, true, "Active", "sub-1", true)]
    public async Task UntrustedExpiredRevokedUnconfirmedInactiveForeignOrMissingCannotWrite(
        string? subject, bool gate, bool confirmed, string status, string localSubject, bool missing)
    {
        var repository = new CaptureRepository();
        using var factory = Factory(repository, subject, gate, confirmed, status, localSubject, missing);
        using var response = await Patch(factory, "{\"displayName\":\"Hacker\"}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        repository.CallCount.Should().Be(0);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("secret-token")
            .And.NotContain("Hacker");
    }

    [Theory]
    [InlineData("{\"displayName\":\"Name\",\"roles\":[\"Admin\"]}")]
    [InlineData("{\"phone\":\"1234567890\",\"accountStatus\":\"Active\"}")]
    [InlineData("{\"displayName\":\"Name\",\"avatarUrl\":\"https://public.example/a\"}")]
    [InlineData("{\"displayName\":\"Name\",\"mediaAssetId\":\"pending\"}")]
    public async Task MixedPrivilegedPayloadNeverReachesRepository(string body)
    {
        var repository = new CaptureRepository();
        using var factory = Factory(repository, "sub-1", true, true, "Active", "sub-1", false);
        using var response = await Patch(factory, body);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        repository.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task ClientSuppliedIdCannotRedirectUpdate()
    {
        var repository = new CaptureRepository();
        using var factory = Factory(repository, "sub-1", true, true, "Active", "sub-1", false);
        using var response = await Patch(factory, "{\"phone\":\"1234567890\"}", "?userId=other-sub");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        repository.Subject.Should().Be("sub-1");
        repository.LocalId.Should().Be("local-id");
        (await response.Content.ReadAsStringAsync()).Should().NotContain("secret-token")
            .And.NotContain("accountStatus").And.NotContain("supabaseUserId");
    }

    [Fact]
    public async Task MissingBearerNeverWrites()
    {
        var repository = new CaptureRepository();
        using var factory = Factory(repository, "sub-1", true, true, "Active", "sub-1", false);
        using var client = factory.CreateClient();
        using var response = await client.PatchAsync("/api/v1/users/me",
            new StringContent("{\"phone\":\"1234567890\"}", Encoding.UTF8, "application/json"));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        repository.CallCount.Should().Be(0);
    }

    private static WebApplicationFactory<Program> Factory(CaptureRepository repository, string? subject,
        bool gate, bool confirmed, string status, string localSubject, bool missing) =>
        SessionSecurityTests.Factory(subject, gate, confirmed, status, localSubject, missing)
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IProfileUpdateRepository>();
                services.AddSingleton<IProfileUpdateRepository>(repository);
            }));

    private static async Task<HttpResponseMessage> Patch(WebApplicationFactory<Program> factory, string body,
        string query = "")
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "secret-token");
        return await client.PatchAsync("/api/v1/users/me" + query,
            new StringContent(body, Encoding.UTF8, "application/json"));
    }

    private sealed class CaptureRepository : IProfileUpdateRepository
    {
        public int CallCount { get; private set; }
        public string? Subject { get; private set; }
        public string? LocalId { get; private set; }
        public Task<ProfileUpdateResult> UpdateOwnAsync(string subject, string localId, long expectedVersion,
            string? displayName, bool changeDisplayName, string? phone, bool changePhone,
            CancellationToken cancellationToken)
        {
            CallCount++;
            Subject = subject;
            LocalId = localId;
            return Task.FromResult(new ProfileUpdateResult(ProfileUpdateOutcome.Updated,
                new LocalLoginProfile("local-id", "sub-1", "a@uttt.edu.mx", "Active",
                    ["Buyer"], changeDisplayName ? displayName : "Alumna", changePhone ? phone : null,
                    Version: expectedVersion + 1)));
        }
    }
}
