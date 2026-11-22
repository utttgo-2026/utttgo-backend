using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using UTTTGO.Api.Modules.Identity.Application;
using Xunit;

namespace UtttGo.IntegrationTests;

public sealed class DependencyCompositionTests
{
    [Fact]
    public async Task UsersControllerCanResolveWithAccountDeletionDependencies()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("MONGODB_URI", "mongodb://localhost:27017");
            builder.UseSetting("REDIS_URL", "redis://localhost:6379");
        });
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IDeleteOwnAccountService>().Should().NotBeNull();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/users/me");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }
}
