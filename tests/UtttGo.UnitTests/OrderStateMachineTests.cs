using FluentAssertions;
using UTTTGO.Api.Modules.Orders.Application;
using Xunit;

namespace UtttGo.UnitTests;

public sealed class OrderStateMachineTests
{
    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Accepted)]
    [InlineData(OrderStatus.Pending, OrderStatus.Rejected)]
    [InlineData(OrderStatus.Pending, OrderStatus.CancelledByBuyer)]
    [InlineData(OrderStatus.Accepted, OrderStatus.InPreparation)]
    [InlineData(OrderStatus.InPreparation, OrderStatus.Ready)]
    [InlineData(OrderStatus.Ready, OrderStatus.Delivered)]
    public void AllowedTransitionsAreAccepted(string from, string to)
    {
        OrderStatus.CanTransition(from, to).Should().BeTrue();
    }

    [Theory]
    [InlineData(OrderStatus.Accepted, OrderStatus.CancelledByBuyer)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Accepted)]
    [InlineData(OrderStatus.Pending, OrderStatus.Delivered)]
    [InlineData(OrderStatus.Pending, OrderStatus.InPreparation)]
    [InlineData(OrderStatus.Ready, OrderStatus.Rejected)]
    [InlineData(OrderStatus.Pending, OrderStatus.Pending)]
    public void ForbiddenTransitionsAreRejected(string from, string to)
    {
        OrderStatus.CanTransition(from, to).Should().BeFalse();
    }

    [Fact]
    public void TerminalStatesAreIdentified()
    {
        OrderStatus.IsTerminal(OrderStatus.Delivered).Should().BeTrue();
        OrderStatus.IsTerminal(OrderStatus.Rejected).Should().BeTrue();
        OrderStatus.IsTerminal(OrderStatus.CancelledByBuyer).Should().BeTrue();
        OrderStatus.IsTerminal(OrderStatus.Pending).Should().BeFalse();
        OrderStatus.IsTerminal(OrderStatus.Ready).Should().BeFalse();
    }

    [Fact]
    public void OnlyKnownStatusesAreAccepted()
    {
        OrderStatus.IsKnown("Pending").Should().BeTrue();
        OrderStatus.IsKnown("Delivered").Should().BeTrue();
        OrderStatus.IsKnown("Inventado").Should().BeFalse();
        OrderStatus.IsKnown(null).Should().BeFalse();
    }
}
