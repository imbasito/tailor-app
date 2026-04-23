using STailor.Core.Domain.Entities;
using STailor.Core.Domain.Enums;
using STailor.Core.Domain.Exceptions;

namespace STailor.Core.Tests.Domain;

public sealed class OrderTests
{
    [Fact]
    public void TransitionTo_ValidPath_AllowsExpectedProgression()
    {
        var order = new Order(
            customerProfileId: Guid.NewGuid(),
            garmentType: "Suit",
            measurementSnapshotJson: "{\"Chest\":40}",
            amountCharged: 3000m,
            receivedAtUtc: new DateTimeOffset(2026, 4, 18, 0, 0, 0, TimeSpan.Zero),
            dueAtUtc: new DateTimeOffset(2026, 4, 25, 0, 0, 0, TimeSpan.Zero));

        order.TransitionTo(OrderStatus.InProgress);
        order.TransitionTo(OrderStatus.TrialFitting);
        order.TransitionTo(OrderStatus.Rework);
        order.TransitionTo(OrderStatus.Ready);
        order.TransitionTo(OrderStatus.Delivered);

        Assert.Equal(OrderStatus.Delivered, order.Status);
    }

    [Fact]
    public void TransitionTo_TrialFittingToReadyWithoutRework_ThrowsDomainRuleViolationException()
    {
        var order = new Order(
            customerProfileId: Guid.NewGuid(),
            garmentType: "Suit",
            measurementSnapshotJson: "{\"Chest\":40}",
            amountCharged: 3000m,
            receivedAtUtc: new DateTimeOffset(2026, 4, 18, 0, 0, 0, TimeSpan.Zero),
            dueAtUtc: new DateTimeOffset(2026, 4, 25, 0, 0, 0, TimeSpan.Zero));

        order.TransitionTo(OrderStatus.InProgress);
        order.TransitionTo(OrderStatus.TrialFitting);

        Assert.Throws<DomainRuleViolationException>(() => order.TransitionTo(OrderStatus.Ready));
    }

    [Fact]
    public void TransitionTo_InvalidPath_ThrowsDomainRuleViolationException()
    {
        var order = new Order(
            customerProfileId: Guid.NewGuid(),
            garmentType: "Suit",
            measurementSnapshotJson: "{\"Chest\":40}",
            amountCharged: 3000m,
            receivedAtUtc: new DateTimeOffset(2026, 4, 18, 0, 0, 0, TimeSpan.Zero),
            dueAtUtc: new DateTimeOffset(2026, 4, 25, 0, 0, 0, TimeSpan.Zero));

        Assert.Throws<DomainRuleViolationException>(() => order.TransitionTo(OrderStatus.Delivered));
    }

    [Fact]
    public void TransitionTo_SameStatus_ThrowsDomainRuleViolationException()
    {
        var order = new Order(
            customerProfileId: Guid.NewGuid(),
            garmentType: "Suit",
            measurementSnapshotJson: "{\"Chest\":40}",
            amountCharged: 3000m,
            receivedAtUtc: new DateTimeOffset(2026, 4, 18, 0, 0, 0, TimeSpan.Zero),
            dueAtUtc: new DateTimeOffset(2026, 4, 25, 0, 0, 0, TimeSpan.Zero));

        var exception = Assert.Throws<DomainRuleViolationException>(() => order.TransitionTo(OrderStatus.New));
        Assert.Contains("already in status", exception.Message);
    }

    [Fact]
    public void TransitionTo_BackwardStatus_ThrowsDomainRuleViolationException()
    {
        var order = new Order(
            customerProfileId: Guid.NewGuid(),
            garmentType: "Suit",
            measurementSnapshotJson: "{\"Chest\":40}",
            amountCharged: 3000m,
            receivedAtUtc: new DateTimeOffset(2026, 4, 18, 0, 0, 0, TimeSpan.Zero),
            dueAtUtc: new DateTimeOffset(2026, 4, 25, 0, 0, 0, TimeSpan.Zero));

        order.TransitionTo(OrderStatus.InProgress);

        var exception = Assert.Throws<DomainRuleViolationException>(() => order.TransitionTo(OrderStatus.New));
        Assert.Contains("Invalid sequential transition", exception.Message);
    }

    [Fact]
    public void ApplyPayment_OverPayment_ThrowsDomainRuleViolationException()
    {
        var order = new Order(
            customerProfileId: Guid.NewGuid(),
            garmentType: "Suit",
            measurementSnapshotJson: "{\"Chest\":40}",
            amountCharged: 2000m,
            receivedAtUtc: new DateTimeOffset(2026, 4, 18, 0, 0, 0, TimeSpan.Zero),
            dueAtUtc: new DateTimeOffset(2026, 4, 25, 0, 0, 0, TimeSpan.Zero));

        order.ApplyPayment(1500m, DateTimeOffset.UtcNow);

        Assert.Throws<DomainRuleViolationException>(() =>
            order.ApplyPayment(600m, DateTimeOffset.UtcNow));
    }
}
