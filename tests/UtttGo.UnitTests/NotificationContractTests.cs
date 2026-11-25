using FluentAssertions;
using UTTTGO.Api.Modules.Notifications.Application;
using UTTTGO.Api.Modules.Notifications.Contracts;
using UTTTGO.Api.Modules.Notifications.Validators;
using Xunit;

namespace UtttGo.UnitTests;

public sealed class NotificationContractTests
{
    [Fact]
    public async Task DeviceTokenRequestRequiresSupportedPlatformAndBoundedFields()
    {
        var validator = new RegisterDeviceTokenRequestValidator();
        var invalid = await validator.ValidateAsync(new RegisterDeviceTokenRequest("", "", "web", new string('x', 33)));
        invalid.IsValid.Should().BeFalse();
        (await validator.ValidateAsync(new RegisterDeviceTokenRequest("fcm-token", "installation", "ios", "1.0.0"))).IsValid.Should().BeTrue();
    }

    [Fact]
    public void RequestDoesNotOfferClientIdentityOrRoleFields()
    {
        typeof(RegisterDeviceTokenRequest).GetProperties().Select(x => x.Name)
            .Should().NotContain(new[] { "UserId", "Role", "AccountStatus" });
    }

    [Fact]
    public void OrderEventKeyIsDeterministicAcrossRetries()
    {
        NotificationEventKeys.For("order.status.changed", "order-1", "Ready")
            .Should().Be("order.status.changed:order-1:Ready");
    }
}
