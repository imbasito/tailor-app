namespace STailor.Core.Application.ReadModels;

/// <summary>
/// Outstanding dues/receivables report.
/// </summary>
public class OutstandingDuesReport
{
    /// <summary>
    /// Report generation timestamp.
    /// </summary>
    public DateTime GeneratedAt { get; set; }

    /// <summary>
    /// Total number of orders with outstanding balance.
    /// </summary>
    public int TotalOrdersWithDues { get; set; }

    /// <summary>
    /// Total outstanding amount across all orders.
    /// </summary>
    public decimal TotalOutstandingAmount { get; set; }

    /// <summary>
    /// Total amount already collected.
    /// </summary>
    public decimal TotalCollectedAmount { get; set; }

    /// <summary>
    /// Collection rate percentage.
    /// </summary>
    public decimal CollectionRatePercent { get; set; }

    /// <summary>
    /// Number of overdue orders.
    /// </summary>
    public int OverdueCount { get; set; }

    /// <summary>
    /// Total amount overdue.
    /// </summary>
    public decimal OverdueAmount { get; set; }

    /// <summary>
    /// Summary by status.
    /// </summary>
    public List<DuesByStatusSummary> ByStatus { get; set; } = new();

    /// <summary>
    /// Individual orders with dues.
    /// </summary>
    public List<OutstandingDueItem> Orders { get; set; } = new();
}

/// <summary>
/// Dues summary grouped by status.
/// </summary>
public class DuesByStatusSummary
{
    /// <summary>
    /// Order status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Number of orders.
    /// </summary>
    public int OrderCount { get; set; }

    /// <summary>
    /// Total outstanding amount.
    /// </summary>
    public decimal OutstandingAmount { get; set; }
}

/// <summary>
/// Individual outstanding due item.
/// </summary>
public class OutstandingDueItem
{
    /// <summary>
    /// Order ID.
    /// </summary>
    public Guid OrderId { get; set; }

    /// <summary>
    /// Order number.
    /// </summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>
    /// Customer ID.
    /// </summary>
    public Guid CustomerId { get; set; }

    /// <summary>
    /// Customer name.
    /// </summary>
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>
    /// Customer phone.
    /// </summary>
    public string CustomerPhone { get; set; } = string.Empty;

    /// <summary>
    /// Order status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Amount charged.
    /// </summary>
    public decimal AmountCharged { get; set; }

    /// <summary>
    /// Total paid to date.
    /// </summary>
    public decimal TotalPaid { get; set; }

    /// <summary>
    /// Balance due.
    /// </summary>
    public decimal BalanceDue { get; set; }

    /// <summary>
    /// Expected/delivery date.
    /// </summary>
    public DateTime? DueDate { get; set; }

    /// <summary>
    /// Days overdue (negative if not yet due).
    /// </summary>
    public int DaysOverdue { get; set; }

    /// <summary>
    /// Whether the order is overdue.
    /// </summary>
    public bool IsOverdue { get; set; }

    /// <summary>
    /// Last payment date.
    /// </summary>
    public DateTime? LastPaymentDate { get; set; }

    /// <summary>
    /// Order received date.
    /// </summary>
    public DateTime ReceivedAt { get; set; }
}
