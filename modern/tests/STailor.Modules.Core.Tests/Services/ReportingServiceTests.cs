using STailor.Core.Application.Abstractions.Services;
using STailor.Core.Domain.Entities;
using STailor.Core.Domain.Enums;
using STailor.Modules.Core.Services;
using STailor.Modules.Core.Tests.Fakes;

namespace STailor.Modules.Core.Tests.Services;

public sealed class ReportingServiceTests
{
    [Fact]
    public async Task GetOperationsReportAsync_SummarizesOrdersPaymentsAndLateWork()
    {
        var orderRepository = new InMemoryOrderRepository();
        var customerRepository = new InMemoryCustomerProfileRepository();
        var clock = new FakeClock(new DateTimeOffset(2026, 4, 23, 10, 0, 0, TimeSpan.Zero));
        var customer = new CustomerProfile("Usman Akhtar", "+923130000224", "Quetta");
        customer.StampCreated(clock.UtcNow, "owner-admin");
        await customerRepository.AddAsync(customer);

        var lateOrder = new Order(
            customer.Id,
            "Trouser",
            "{\"Waist\":34}",
            3200m,
            clock.UtcNow.AddDays(-14),
            clock.UtcNow.AddDays(-5));
        lateOrder.ApplyPayment(1200m, clock.UtcNow.AddDays(-10), "Advance");
        await orderRepository.AddAsync(lateOrder);

        var deliveredOrder = new Order(
            customer.Id,
            "Shirt",
            "{\"Chest\":40}",
            1800m,
            clock.UtcNow.AddDays(-8),
            clock.UtcNow.AddDays(-1));
        deliveredOrder.ApplyPayment(1800m, clock.UtcNow.AddDays(-2), "Paid");
        MoveTo(deliveredOrder, OrderStatus.Delivered);
        await orderRepository.AddAsync(deliveredOrder);

        var service = new ReportingService(orderRepository, customerRepository, clock);

        var report = await service.GetOperationsReportAsync();

        Assert.Equal(2, report.TotalOrders);
        Assert.Equal(1, report.OpenOrders);
        Assert.Equal(1, report.DeliveredOrders);
        Assert.Equal(1, report.OverdueOrders);
        Assert.Equal(5000m, report.TotalCharged);
        Assert.Equal(3000m, report.TotalPaid);
        Assert.Equal(2000m, report.TotalBalanceDue);
        Assert.Contains(report.ByStatus, item => item.Status == OrderStatus.Delivered.ToString() && item.OrderCount == 1);
        Assert.Contains(report.ByGarment, item => item.GarmentType == "Trouser" && item.BalanceDue == 2000m);
    }

    [Fact]
    public async Task GetOperationsReportAsync_FiltersBySearchAndHidesDelivered()
    {
        var orderRepository = new InMemoryOrderRepository();
        var customerRepository = new InMemoryCustomerProfileRepository();
        var clock = new FakeClock(new DateTimeOffset(2026, 4, 23, 10, 0, 0, TimeSpan.Zero));

        var customer = new CustomerProfile("Ayesha Noor", "+923000000111", "Lahore");
        customer.StampCreated(clock.UtcNow, "owner-admin");
        await customerRepository.AddAsync(customer);

        var openOrder = new Order(
            customer.Id,
            "Suit",
            "{\"Chest\":38}",
            4500m,
            clock.UtcNow.AddDays(-2),
            clock.UtcNow.AddDays(3));
        await orderRepository.AddAsync(openOrder);

        var deliveredOrder = new Order(
            customer.Id,
            "Shirt",
            "{\"Chest\":38}",
            1500m,
            clock.UtcNow.AddDays(-4),
            clock.UtcNow.AddDays(-1));
        MoveTo(deliveredOrder, OrderStatus.Delivered);
        await orderRepository.AddAsync(deliveredOrder);

        var service = new ReportingService(orderRepository, customerRepository, clock);

        var report = await service.GetOperationsReportAsync(
            new OperationsReportFilter
            {
                SearchText = "suit",
                IncludeDelivered = false,
            });

        var item = Assert.Single(report.Orders);
        Assert.Equal("Suit", item.GarmentType);
        Assert.Equal(1, report.OpenOrders);
        Assert.Equal(0, report.DeliveredOrders);
    }

    [Fact]
    public async Task GetOperationsReportAsync_FiltersByReceivedDateRange()
    {
        var orderRepository = new InMemoryOrderRepository();
        var customerRepository = new InMemoryCustomerProfileRepository();
        var clock = new FakeClock(new DateTimeOffset(2026, 4, 23, 10, 0, 0, TimeSpan.Zero));

        var customer = new CustomerProfile("Noor Nawaz", "+923020000063", "Islamabad");
        customer.StampCreated(clock.UtcNow, "owner-admin");
        await customerRepository.AddAsync(customer);

        await orderRepository.AddAsync(new Order(
            customer.Id,
            "Shirt",
            "{\"Chest\":40}",
            2500m,
            new DateTimeOffset(2026, 4, 4, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 13, 9, 0, 0, TimeSpan.Zero)));

        await orderRepository.AddAsync(new Order(
            customer.Id,
            "Suit",
            "{\"Chest\":42}",
            6500m,
            new DateTimeOffset(2026, 4, 20, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 30, 9, 0, 0, TimeSpan.Zero)));

        var service = new ReportingService(orderRepository, customerRepository, clock);

        var report = await service.GetOperationsReportAsync(
            new OperationsReportFilter
            {
                ReceivedFromUtc = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
                ReceivedToUtc = new DateTimeOffset(2026, 4, 10, 23, 59, 59, TimeSpan.Zero),
            });

        var item = Assert.Single(report.Orders);
        Assert.Equal("Shirt", item.GarmentType);
        Assert.Equal(2500m, report.TotalCharged);
    }

    private static void MoveTo(Order order, OrderStatus targetStatus)
    {
        var progression = new[]
        {
            OrderStatus.InProgress,
            OrderStatus.TrialFitting,
            OrderStatus.Rework,
            OrderStatus.Ready,
            OrderStatus.Delivered,
        };

        foreach (var status in progression)
        {
            order.TransitionTo(status);
            if (status == targetStatus)
            {
                return;
            }
        }
    }
}
