using System.Text.Json;
using STailor.Core.Application.Abstractions.Repositories;
using STailor.Core.Application.Abstractions.Services;
using STailor.Core.Application.ReadModels;
using STailor.Core.Common.Time;
using STailor.Core.Domain.Enums;

namespace STailor.Modules.Core.Services;

/// <summary>
/// Implementation of reporting service for operational reports.
/// </summary>
public class ReportingService : IReportingService
{
    private readonly IOrderRepository _orderRepository;
    private readonly ICustomerProfileRepository _customerRepository;
    private readonly IClock _clock;

    public ReportingService(
        IOrderRepository orderRepository,
        ICustomerProfileRepository customerRepository,
        IClock clock)
    {
        _orderRepository = orderRepository;
        _customerRepository = customerRepository;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<OperationsReport> GetOperationsReportAsync(
        OperationsReportFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        filter ??= new OperationsReportFilter();
        var statusFilter = TryParseStatus(filter.Status, out var parsedStatus)
            ? parsedStatus
            : (OrderStatus?)null;

        var orders = await _orderRepository.GetOrdersForOperationsReportAsync(
            filter.IncludeDelivered,
            statusFilter,
            filter.ReceivedFromUtc,
            filter.ReceivedToUtc,
            cancellationToken);

        var customerIds = orders.Select(o => o.CustomerProfileId).Distinct().ToList();
        var customers = new Dictionary<Guid, CustomerInfo>();
        foreach (var customerId in customerIds)
        {
            var customer = await _customerRepository.GetByIdAsync(customerId, cancellationToken);
            if (customer != null)
            {
                customers[customerId] = new CustomerInfo(customer.FullName, customer.PhoneNumber, customer.City);
            }
        }

        var today = _clock.UtcNow.Date;
        var items = orders.Select(o =>
        {
            customers.TryGetValue(o.CustomerProfileId, out var customer);
            var dueDate = o.DueAtUtc.DateTime;
            var daysLate = (today - dueDate.Date).Days;
            var lastPaymentAt = o.Payments
                .OrderByDescending(p => p.PaidAtUtc)
                .Select(p => (DateTime?)p.PaidAtUtc.DateTime)
                .FirstOrDefault();

            return new OperationsReportOrder
            {
                OrderId = o.Id,
                OrderNumber = $"ORD-{o.Id.ToString()[..8].ToUpperInvariant()}",
                CustomerId = o.CustomerProfileId,
                CustomerName = customer?.Name ?? "Unknown",
                CustomerPhone = customer?.Phone ?? string.Empty,
                City = customer?.City ?? string.Empty,
                GarmentType = o.GarmentType,
                Status = o.Status.ToString(),
                AmountCharged = o.AmountCharged,
                AmountPaid = o.AmountPaid,
                BalanceDue = o.BalanceDue,
                ReceivedAt = o.ReceivedAtUtc.DateTime,
                DueAt = dueDate,
                DaysLate = Math.Max(0, daysLate),
                IsOverdue = o.Status != OrderStatus.Delivered && daysLate > 0,
                LastPaymentAt = lastPaymentAt,
            };
        }).ToList();

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var searchText = filter.SearchText.Trim();
            items = items
                .Where(item =>
                    item.OrderId.ToString().Contains(searchText, StringComparison.OrdinalIgnoreCase)
                    || item.CustomerId.ToString().Contains(searchText, StringComparison.OrdinalIgnoreCase)
                    || item.CustomerName.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                    || item.CustomerPhone.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                    || item.City.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                    || item.GarmentType.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                    || item.Status.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var totalCharged = items.Sum(i => i.AmountCharged);
        var totalPaid = items.Sum(i => i.AmountPaid);

        return new OperationsReport
        {
            GeneratedAt = _clock.UtcNow.DateTime,
            TotalOrders = items.Count,
            OpenOrders = items.Count(i => !string.Equals(i.Status, OrderStatus.Delivered.ToString(), StringComparison.OrdinalIgnoreCase)),
            DeliveredOrders = items.Count(i => string.Equals(i.Status, OrderStatus.Delivered.ToString(), StringComparison.OrdinalIgnoreCase)),
            OverdueOrders = items.Count(i => i.IsOverdue),
            TotalCharged = totalCharged,
            TotalPaid = totalPaid,
            TotalBalanceDue = items.Sum(i => i.BalanceDue),
            CollectionRatePercent = totalCharged > 0m ? Math.Round(totalPaid / totalCharged * 100m, 2) : 0m,
            ByStatus = items
                .GroupBy(i => i.Status)
                .Select(g => new OperationsStatusSummary
                {
                    Status = g.Key,
                    OrderCount = g.Count(),
                    Charged = g.Sum(i => i.AmountCharged),
                    Paid = g.Sum(i => i.AmountPaid),
                    BalanceDue = g.Sum(i => i.BalanceDue),
                })
                .OrderBy(s => TryParseStatus(s.Status, out var status) ? status : OrderStatus.New)
                .ToList(),
            ByGarment = items
                .GroupBy(i => i.GarmentType)
                .Select(g => new OperationsGarmentSummary
                {
                    GarmentType = g.Key,
                    OrderCount = g.Count(),
                    Charged = g.Sum(i => i.AmountCharged),
                    Paid = g.Sum(i => i.AmountPaid),
                    BalanceDue = g.Sum(i => i.BalanceDue),
                })
                .OrderByDescending(s => s.OrderCount)
                .ThenBy(s => s.GarmentType, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Orders = items
                .OrderByDescending(i => i.IsOverdue)
                .ThenBy(i => i.DueAt)
                .ThenBy(i => i.CustomerName, StringComparer.OrdinalIgnoreCase)
                .ToList(),
        };
    }

    /// <inheritdoc />
    public async Task<DailyOrdersReport> GetDailyOrdersReportAsync(DateTime? date = null, CancellationToken cancellationToken = default)
    {
        var reportDate = date?.Date ?? _clock.UtcNow.Date;
        var startOfDay = new DateTimeOffset(reportDate, TimeSpan.Zero);
        var endOfDay = startOfDay.AddDays(1).AddTicks(-1);

        var orders = await _orderRepository.GetOrdersReceivedBetweenAsync(startOfDay, endOfDay, cancellationToken);

        // Load customer details for each order
        var customerIds = orders.Select(o => o.CustomerProfileId).Distinct().ToList();
        var customers = new Dictionary<Guid, CustomerInfo>();
        foreach (var customerId in customerIds)
        {
            var customer = await _customerRepository.GetByIdAsync(customerId, cancellationToken);
            if (customer != null)
            {
                customers[customerId] = new CustomerInfo(customer.FullName, customer.PhoneNumber, customer.City);
            }
        }

        var items = orders.Select(o =>
        {
            customers.TryGetValue(o.CustomerProfileId, out var customer);
            return new DailyOrderItem
            {
                OrderId = o.Id,
                OrderNumber = $"ORD-{o.Id.ToString()[..8].ToUpper()}",
                CustomerName = customer?.Name ?? "Unknown",
                CustomerPhone = customer?.Phone ?? "",
                GarmentType = o.GarmentType,
                AmountCharged = o.AmountCharged,
                AmountPaid = o.AmountPaid,
                BalanceDue = o.BalanceDue,
                DeliveryDate = o.DueAtUtc.DateTime,
                Status = o.Status.ToString(),
                ReceivedAt = o.ReceivedAtUtc.DateTime
            };
        }).ToList();

        var byGarmentType = items
            .GroupBy(i => i.GarmentType)
            .Select(g => new GarmentTypeSummary
            {
                GarmentType = g.Key,
                Count = g.Count(),
                TotalAmount = g.Sum(x => x.AmountCharged)
            })
            .OrderByDescending(g => g.Count)
            .ToList();

        return new DailyOrdersReport
        {
            ReportDate = reportDate,
            TotalOrdersReceived = items.Count,
            TotalAmountCharged = items.Sum(i => i.AmountCharged),
            TotalAmountPaid = items.Sum(i => i.AmountPaid),
            ByGarmentType = byGarmentType,
            Orders = items.OrderByDescending(i => i.ReceivedAt).ToList()
        };
    }

    /// <inheritdoc />
    public async Task<OutstandingDuesReport> GetOutstandingDuesReportAsync(OutstandingDuesFilter? filter = null, CancellationToken cancellationToken = default)
    {
        filter ??= new OutstandingDuesFilter();
        var today = _clock.UtcNow.Date;

        var orders = await _orderRepository.GetOrdersWithBalanceDueAsync(
            filter.MinBalanceDue,
            filter.MaxBalanceDue,
            filter.Status,
            cancellationToken);

        // Load customer details
        var customerIds = orders.Select(o => o.CustomerProfileId).Distinct().ToList();
        var customers = new Dictionary<Guid, CustomerInfo>();
        foreach (var customerId in customerIds)
        {
            var customer = await _customerRepository.GetByIdAsync(customerId, cancellationToken);
            if (customer != null)
            {
                customers[customerId] = new CustomerInfo(customer.FullName, customer.PhoneNumber, customer.City);
            }
        }

        var items = orders.Select(o =>
        {
            customers.TryGetValue(o.CustomerProfileId, out var customer);
            var dueDate = o.DueAtUtc.DateTime;
            var daysOverdue = (today - dueDate).Days;

            // Get last payment date from payments collection
            var lastPaymentDate = o.Payments
                .OrderByDescending(p => p.PaidAtUtc)
                .Select(p => (DateTime?)p.PaidAtUtc.DateTime)
                .FirstOrDefault();

            return new OutstandingDueItem
            {
                OrderId = o.Id,
                OrderNumber = $"ORD-{o.Id.ToString()[..8].ToUpper()}",
                CustomerId = o.CustomerProfileId,
                CustomerName = customer?.Name ?? "Unknown",
                CustomerPhone = customer?.Phone ?? "",
                Status = o.Status.ToString(),
                AmountCharged = o.AmountCharged,
                TotalPaid = o.AmountPaid,
                BalanceDue = o.BalanceDue,
                DueDate = dueDate,
                DaysOverdue = daysOverdue,
                IsOverdue = daysOverdue > 0,
                LastPaymentDate = lastPaymentDate,
                ReceivedAt = o.ReceivedAtUtc.DateTime
            };
        }).ToList();

        // Apply overdue filter
        if (filter.OverdueOnly)
        {
            items = items.Where(i => i.IsOverdue).ToList();
        }

        // Apply ordering
        items = filter.OrderBy?.ToLower() switch
        {
            "duedateasc" => items.OrderBy(i => i.DueDate ?? DateTime.MaxValue).ThenBy(i => i.CustomerName).ToList(),
            "customername" => items.OrderBy(i => i.CustomerName).ThenByDescending(i => i.BalanceDue).ToList(),
            _ => items.OrderByDescending(i => i.BalanceDue).ThenBy(i => i.CustomerName).ToList() // BalanceDesc default
        };

        var byStatus = items
            .GroupBy(i => i.Status)
            .Select(g => new DuesByStatusSummary
            {
                Status = g.Key,
                OrderCount = g.Count(),
                OutstandingAmount = g.Sum(x => x.BalanceDue)
            })
            .OrderByDescending(s => s.OutstandingAmount)
            .ToList();

        var totalCharged = items.Sum(i => i.AmountCharged);
        var totalPaid = items.Sum(i => i.TotalPaid);
        var totalOutstanding = items.Sum(i => i.BalanceDue);

        return new OutstandingDuesReport
        {
            GeneratedAt = _clock.UtcNow.DateTime,
            TotalOrdersWithDues = items.Count,
            TotalOutstandingAmount = totalOutstanding,
            TotalCollectedAmount = totalPaid,
            CollectionRatePercent = totalCharged > 0 ? Math.Round(totalPaid / totalCharged * 100, 2) : 0,
            OverdueCount = items.Count(i => i.IsOverdue),
            OverdueAmount = items.Where(i => i.IsOverdue).Sum(i => i.BalanceDue),
            ByStatus = byStatus,
            Orders = items
        };
    }

    /// <inheritdoc />
    public async Task<CustomerMeasurementHistoryReport> GetCustomerMeasurementHistoryAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var customer = await _customerRepository.GetByIdWithHistoryAsync(customerId, cancellationToken);
        if (customer == null)
        {
            throw new ArgumentException($"Customer with ID {customerId} not found", nameof(customerId));
        }

        var orders = await _orderRepository.GetOrdersByCustomerAsync(customerId, cancellationToken);

        // Parse measurement snapshots from JSON
        var orderSnapshots = orders
            .Where(o => !string.IsNullOrWhiteSpace(o.MeasurementSnapshotJson) && o.MeasurementSnapshotJson != "{}")
            .Select(o =>
            {
                Dictionary<string, decimal>? measurements = null;
                try
                {
                    measurements = JsonSerializer.Deserialize<Dictionary<string, decimal>>(o.MeasurementSnapshotJson);
                }
                catch
                {
                    measurements = new Dictionary<string, decimal>();
                }

                return new OrderMeasurementSnapshot
                {
                    OrderId = o.Id,
                    OrderNumber = $"ORD-{o.Id.ToString()[..8].ToUpper()}",
                    OrderDate = o.ReceivedAtUtc.DateTime,
                    GarmentType = o.GarmentType,
                    Measurements = measurements ?? new Dictionary<string, decimal>(),
                    Notes = null,
                    DeviatedFromBaseline = false // Simplified for now
                };
            })
            .Where(o => o.Measurements.Count > 0)
            .OrderByDescending(o => o.OrderDate)
            .ToList();

        // Get baseline from latest order or empty
        MeasurementSnapshot? baseline = null;
        var latestOrderWithMeasurements = orderSnapshots.FirstOrDefault();
        if (latestOrderWithMeasurements != null)
        {
            baseline = new MeasurementSnapshot
            {
                LastUpdated = latestOrderWithMeasurements.OrderDate,
                GarmentType = latestOrderWithMeasurements.GarmentType,
                Measurements = latestOrderWithMeasurements.Measurements,
                Notes = latestOrderWithMeasurements.Notes
            };
        }

        return new CustomerMeasurementHistoryReport
        {
            CustomerId = customer.Id,
            CustomerName = customer.FullName,
            CustomerPhone = customer.PhoneNumber,
            CustomerNotes = null,
            CurrentBaseline = baseline,
            OrderHistory = orderSnapshots,
            TotalOrders = orders.Count,
            LastOrderDate = orders.OrderByDescending(o => o.ReceivedAtUtc).FirstOrDefault()?.ReceivedAtUtc.DateTime
        };
    }

    /// <inheritdoc />
    public async Task<DeliveryQueueReport> GetDeliveryQueueAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var orders = await _orderRepository.GetOrdersForDeliveryWindowAsync(fromDate, toDate, cancellationToken);
        var today = _clock.UtcNow.Date;

        // Load customer details
        var customerIds = orders.Select(o => o.CustomerProfileId).Distinct().ToList();
        var customers = new Dictionary<Guid, CustomerInfo>();
        foreach (var customerId in customerIds)
        {
            var customer = await _customerRepository.GetByIdAsync(customerId, cancellationToken);
            if (customer != null)
            {
                customers[customerId] = new CustomerInfo(customer.FullName, customer.PhoneNumber, customer.City);
            }
        }

        var items = orders.Select(o =>
        {
            customers.TryGetValue(o.CustomerProfileId, out var customer);
            var deliveryDate = o.DueAtUtc.DateTime;
            var daysUntil = (deliveryDate - today).Days;
            var isReady = o.Status == OrderStatus.Ready || o.Status == OrderStatus.Delivered;

            return new DeliveryQueueItem
            {
                OrderId = o.Id,
                OrderNumber = $"ORD-{o.Id.ToString()[..8].ToUpper()}",
                CustomerName = customer?.Name ?? "Unknown",
                CustomerPhone = customer?.Phone ?? "",
                GarmentType = o.GarmentType,
                Status = o.Status.ToString(),
                IsReady = isReady,
                AmountCharged = o.AmountCharged,
                TotalPaid = o.AmountPaid,
                BalanceDue = o.BalanceDue,
                DeliveryDate = deliveryDate,
                IsBalanceSettled = o.BalanceDue <= 0,
                DaysUntilDelivery = daysUntil
            };
        }).ToList();

        var byDate = items
            .GroupBy(i => i.DeliveryDate.Date)
            .Select(g => new DeliveryDateGroup
            {
                DeliveryDate = g.Key,
                OrderCount = g.Count(),
                TotalBalanceDue = g.Sum(x => x.BalanceDue),
                Orders = g.OrderBy(o => o.CustomerName).ToList()
            })
            .OrderBy(g => g.DeliveryDate)
            .ToList();

        return new DeliveryQueueReport
        {
            FromDate = fromDate,
            ToDate = toDate,
            TotalOrders = items.Count,
            ReadyForDeliveryCount = items.Count(i => i.IsReady),
            InProgressCount = items.Count(i => !i.IsReady),
            TotalBalanceDue = items.Sum(i => i.BalanceDue),
            ByDeliveryDate = byDate
        };
    }

    private static bool TryParseStatus(string? value, out OrderStatus status)
    {
        status = OrderStatus.New;

        if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "Any", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var normalized = value.Trim().Replace(" ", string.Empty).Replace("/", string.Empty).Replace("-", string.Empty);
        if (Enum.TryParse<OrderStatus>(normalized, true, out status))
        {
            return true;
        }

        status = normalized.ToLowerInvariant() switch
        {
            "progress" or "inprogress" => OrderStatus.InProgress,
            "trial" or "fitting" or "trialfitting" => OrderStatus.TrialFitting,
            _ => OrderStatus.New,
        };

        return normalized.Equals("progress", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("inprogress", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("trial", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("fitting", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("trialfitting", StringComparison.OrdinalIgnoreCase);
    }

    private record CustomerInfo(string Name, string Phone, string City);
}
