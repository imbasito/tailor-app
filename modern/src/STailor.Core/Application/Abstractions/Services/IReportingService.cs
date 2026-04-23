using STailor.Core.Application.ReadModels;

namespace STailor.Core.Application.Abstractions.Services;

/// <summary>
/// Service for generating operational reports.
/// </summary>
public interface IReportingService
{
    /// <summary>
    /// Gets the full operations report with financial and order detail.
    /// </summary>
    Task<OperationsReport> GetOperationsReportAsync(
        OperationsReportFilter? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets daily orders report with optional date filter.
    /// </summary>
    Task<DailyOrdersReport> GetDailyOrdersReportAsync(DateTime? date = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets outstanding dues/receivables report.
    /// </summary>
    Task<OutstandingDuesReport> GetOutstandingDuesReportAsync(OutstandingDuesFilter? filter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets customer measurement history report.
    /// </summary>
    Task<CustomerMeasurementHistoryReport> GetCustomerMeasurementHistoryAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets delivery queue for a specific date range.
    /// </summary>
    Task<DeliveryQueueReport> GetDeliveryQueueAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
}

/// <summary>
/// Filter options for outstanding dues report.
/// </summary>
public class OutstandingDuesFilter
{
    /// <summary>
    /// Minimum balance due to include.
    /// </summary>
    public decimal? MinBalanceDue { get; set; }

    /// <summary>
    /// Maximum balance due to include.
    /// </summary>
    public decimal? MaxBalanceDue { get; set; }

    /// <summary>
    /// Filter by order status.
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Only include overdue orders.
    /// </summary>
    public bool OverdueOnly { get; set; }

    /// <summary>
    /// Order by field (BalanceDesc, DueDateAsc, CustomerName).
    /// </summary>
    public string OrderBy { get; set; } = "BalanceDesc";
}

/// <summary>
/// Filter options for the complete operations report.
/// </summary>
public class OperationsReportFilter
{
    public string? SearchText { get; set; }

    public string? Status { get; set; }

    public bool IncludeDelivered { get; set; } = true;

    public DateTimeOffset? ReceivedFromUtc { get; set; }

    public DateTimeOffset? ReceivedToUtc { get; set; }
}
