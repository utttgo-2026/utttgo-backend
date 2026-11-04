using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UTTTGO.Api.Modules.Identity.Application;
using Xunit;

namespace UtttGo.IntegrationTests;

public sealed class UpdateOwnProfileEndpointTests
{
    private const string Route = "/api/v1/users/me";

    [Theory]
    [InlineData("{\"displayName\":\" New Name \"}", "New Name", "1234567890")]
    [InlineData("{\"phone\":\"+521234567890\"}", "Alumna", "+521234567890")]
    [InlineData("{\"displayName\":\"New Name\",\"phone\":\"1234567891\"}", "New Name", "1234567891")]
    public async Task AuthorizedPatchUpdatesOnlyProvidedFields(string body, string name, string phone)
    {
        var repository = new UpdateDouble();
        using var factory = Factory(repository);
        using var response = await Patch(factory, body);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("displayName").GetString().Should().Be(name);
        json.RootElement.GetProperty("phone").GetString().Should().Be(phone);
        json.RootElement.GetProperty("avatarUrl").ValueKind.Should().Be(JsonValueKind.Null);
        json.RootElement.EnumerateObject().Select(field => field.Name).Should().BeEquivalentTo(
            ["id", "displayName", "email", "phone", "avatarUrl", "roles"]);
        repository.LastSubject.Should().Be("sub-1");
    }

    [Theory]
    [InlineData("{\"roles\":[\"Admin\"]}")]
    [InlineData("{\"email\":\"attacker@uttt.edu.mx\"}")]
    [InlineData("{\"accountStatus\":\"Active\"}")]
    [InlineData("{\"sellerStatus\":\"Active\"}")]
    [InlineData("{\"userId\":\"other\"}")]
    [InlineData("{\"emailVerified\":true}")]
    [InlineData("{\"avatarUrl\":\"https://arbitrary.example/image\"}")]
    [InlineData("{\"mediaAssetId\":\"pending\"}")]
    [InlineData("{\"displayName\":null}")]
    [InlineData("{\"phone\":null}")]
    [InlineData("{\"displayName\":\"A\",\"displayName\":\"B\"}")]
    [InlineData("{\"displayName\":\"\"}")]
    [InlineData("{\"phone\":\"abc\"}")]
    [InlineData("{\"DisplayName\":\"Hacker\"}")]
    [InlineData("{\"Phone\":\"1234567890\"}")]
    [InlineData("{\"displayNAME\":\"Hacker\"}")]
    [InlineData("{\"displayName\":\"Valid\",\"unknown\":true}")]
    [InlineData("{\"displayName\":\"Valid\",\"phone\":null}")]
    [InlineData("{\"displayName\":42}")]
    [InlineData("{\"phone\":[\"1234567890\"]}")]
    [InlineData("{}")]
    [InlineData("{\"displayName\":")]
    public async Task InvalidOrForbiddenPatchIsSanitized400WithoutWrite(string body)
    {
        var repository = new UpdateDouble();
        using var factory = Factory(repository);
        using var response = await Patch(factory, body);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("correlationId")
            .And.NotContain("attacker@uttt.edu.mx").And.NotContain("arbitrary.example");
        repository.LastSubject.Should().BeNull();
    }

    [Fact]
    public async Task UnconfiguredDependenciesFailClosed()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var response = await Patch(factory, "{\"displayName\":\"New Name\"}");
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Theory]
    [InlineData(ProfileUpdateOutcome.Conflict, HttpStatusCode.Conflict)]
    [InlineData(ProfileUpdateOutcome.Missing, HttpStatusCode.Unauthorized)]
    public async Task ConcurrentChangeOrDeletedProfileDoesNotReturnSuccess(ProfileUpdateOutcome outcome,
        HttpStatusCode expected)
    {
        using var factory = Factory(new UpdateDouble(outcome));
        using var response = await Patch(factory, "{\"displayName\":\"New Name\"}");
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task PersistenceFailureReturns503WithoutSecrets()
    {
        using var factory = Factory(new UpdateDouble(fail: true));
        using var response = await Patch(factory, "{\"displayName\":\"New Name\"}");
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("database secret")
            .And.NotContain("test-jwt");
    }

    [Theory]
    [InlineData(1, 10, HttpStatusCode.OK)]
    [InlineData(100, 15, HttpStatusCode.OK)]
    [InlineData(101, 10, HttpStatusCode.BadRequest)]
    [InlineData(1, 9, HttpStatusCode.BadRequest)]
    [InlineData(1, 16, HttpStatusCode.BadRequest)]
    public async Task CandidateNameAndPhoneBoundariesHaveExpectedHttpStatus(int nameLength, int phoneLength,
        HttpStatusCode expected)
    {
        var repository = new UpdateDouble();
        using var factory = Factory(repository);
        var body = JsonSerializer.Serialize(new
        {
            displayName = new string('N', nameLength),
            phone = new string('1', phoneLength)
        });
        using var response = await Patch(factory, body);
        response.StatusCode.Should().Be(expected);
        if (expected == HttpStatusCode.OK)
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            json.RootElement.GetProperty("displayName").GetString()!.Length.Should().Be(nameLength);
            json.RootElement.GetProperty("phone").GetString()!.Length.Should().Be(phoneLength);
            repository.LastSubject.Should().Be("sub-1");
        }
        else repository.LastSubject.Should().BeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PatchedProfileCanBeReadBackWithSameAvatarPolicyAsGet(bool mediaConfigured)
    {
        var store = new StatefulProfileStore(new LocalLoginProfile("local-id", "sub-1", "a@uttt.edu.mx",
            "Active", ["Buyer"], "Alumna", "1234567890", "approved-owned", 4));
        const string temporaryUrl = "https://private.example/short-lived-sas";
        using var baseFactory = SessionEndpointTests.Factory(avatarAssetId: "approved-owned",
            avatarUrl: mediaConfigured ? temporaryUrl : null);
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ILoginProfileRepository>();
            services.RemoveAll<IProfileUpdateRepository>();
            services.AddSingleton<ILoginProfileRepository>(store);
            services.AddSingleton<IProfileUpdateRepository>(store);
        }));

        using var before = await OwnProfileEndpointTests.Get(factory);
        using var beforeJson = JsonDocument.Parse(await before.Content.ReadAsStringAsync());
        beforeJson.RootElement.GetProperty("avatarUrl").GetString().Should().Be(
            mediaConfigured ? temporaryUrl : null);

        using var patch = await Patch(factory, "{\"displayName\":\"Updated\"}");
        patch.StatusCode.Should().Be(HttpStatusCode.OK);
        using var patchJson = JsonDocument.Parse(await patch.Content.ReadAsStringAsync());
        patchJson.RootElement.GetProperty("avatarUrl").GetString().Should().Be(
            mediaConfigured ? temporaryUrl : null);
        patchJson.RootElement.GetProperty("displayName").GetString().Should().Be("Updated");

        using var after = await OwnProfileEndpointTests.Get(factory);
        using var afterJson = JsonDocument.Parse(await after.Content.ReadAsStringAsync());
        afterJson.RootElement.GetProperty("displayName").GetString().Should().Be("Updated");
        afterJson.RootElement.GetProperty("phone").GetString().Should().Be("1234567890");
        afterJson.RootElement.GetProperty("avatarUrl").GetString().Should().Be(
            mediaConfigured ? temporaryUrl : null);
        store.Snapshot.Version.Should().Be(5);
        store.Snapshot.AvatarMediaAssetId.Should().Be("approved-owned");
    }

    private static WebApplicationFactory<Program> Factory(UpdateDouble repository) =>
        SessionEndpointTests.Factory().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IProfileUpdateRepository>();
            services.AddSingleton<IProfileUpdateRepository>(repository);
        }));

    private static async Task<HttpResponseMessage> Patch(WebApplicationFactory<Program> factory, string body)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-jwt");
        using var request = new HttpRequestMessage(HttpMethod.Patch, Route)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        return await client.SendAsync(request);
    }

    private sealed class UpdateDouble(ProfileUpdateOutcome outcome = ProfileUpdateOutcome.Updated, bool fail = false)
        : IProfileUpdateRepository
    {
        public string? LastSubject { get; private set; }
        public Task<ProfileUpdateResult> UpdateOwnAsync(string subject, string localId, long expectedVersion,
            string? displayName, bool changeDisplayName, string? phone, bool changePhone,
            CancellationToken cancellationToken)
        {
            LastSubject = subject;
            if (fail) throw new InvalidOperationException("database secret");
            if (outcome != ProfileUpdateOutcome.Updated) return Task.FromResult(new ProfileUpdateResult(outcome));
            var updated = new LocalLoginProfile("local-id", "sub-1", "a@uttt.edu.mx", "Active",
                ["Buyer"], changeDisplayName ? displayName : "Alumna",
                changePhone ? phone : "1234567890", Version: expectedVersion + 1);
            return Task.FromResult(new ProfileUpdateResult(ProfileUpdateOutcome.Updated, updated));
        }
    }

    private sealed class StatefulProfileStore(LocalLoginProfile initial)
        : ILoginProfileRepository, IProfileUpdateRepository
    {
        private readonly object _sync = new();
        private LocalLoginProfile _profile = initial;
        public LocalLoginProfile Snapshot { get { lock (_sync) return _profile; } }

        public Task<LocalLoginProfile?> FindBySubjectAsync(string subject, CancellationToken cancellationToken)
        {
            lock (_sync) return Task.FromResult<LocalLoginProfile?>(
                subject == _profile.SupabaseUserId ? _profile : null);
        }

        public Task<ProfileUpdateResult> UpdateOwnAsync(string subject, string localId, long expectedVersion,
            string? displayName, bool changeDisplayName, string? phone, bool changePhone,
            CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                if (subject != _profile.SupabaseUserId || localId != _profile.Id ||
                    _profile.AccountStatus != "Active")
                    return Task.FromResult(new ProfileUpdateResult(ProfileUpdateOutcome.Missing));
                if (expectedVersion != _profile.Version)
                    return Task.FromResult(new ProfileUpdateResult(ProfileUpdateOutcome.Conflict));
                _profile = _profile with
                {
                    DisplayName = changeDisplayName ? displayName : _profile.DisplayName,
                    Phone = changePhone ? phone : _profile.Phone,
                    Version = _profile.Version + 1
                };
                return Task.FromResult(new ProfileUpdateResult(ProfileUpdateOutcome.Updated, _profile));
            }
        }
    }
}
