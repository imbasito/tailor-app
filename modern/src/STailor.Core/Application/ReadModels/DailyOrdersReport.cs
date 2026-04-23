namespace STailor.Core.Application.ReadModels;

/// <summary>
/// Daily orders report showing orders received and their status breakdown.
/// </summary>
public class DailyOrdersReport
{
    /// <summary>
    /// Report date.
    /// </summary>
    public DateTime ReportDate { get; set; }

    /// <summary>
    /// Total orders received on the report date.
    /// </summary>
    public int TotalOrdersReceived { get; set; }

    /// <summary>
    /// Total amount charged for orders received.
    /// </summary>
    public decimal TotalAmountCharged { get; set; }

    /// <summary>
    /// Total amount paid (deposits) for orders received.
    /// </summary>
    public decimal TotalAmountPaid { get; set; }

    /// <summary>
    /// Breakdown by garment type or category.
    /// </summary>
    public List<GarmentTypeSummary> ByGarmentType { get; set; } = new();

    /// <summary>
    /// Individual orders received.
    /// </summary>
    public List<DailyOrderItem> Orders { get; set; } = new();
}

/// <summary>
/// Summary for a specific garment type.
/// </summary>
public class GarmentTypeSummary
{
    /// <summary>
    /// Garment type name.
    /// </summary>
    public string GarmentType { get; set; } = string.Empty;

    /// <summary>
    /// Count of orders.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Total amount charged.
    /// </summary>
    public decimal TotalAmount { get; set; }
}

/// <summary>
/// Individual order item in daily report.
/// </summary>
public class DailyOrderItem
{
    /// <summary>
    /// Order ID.
    /// </summary>
    public Guid OrderId { get; set; }

    /// <summary>
    /// Order number or reference.
    /// </summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>
    /// Customer name.
    /// </summary>
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>
    /// Customer phone number.
    /// </summary>
    public string CustomerPhone { get; set; } = string.Empty;

    /// <summary>
    /// Garment type.
    /// </summary>
    public string GarmentType { get; set; } = string.Empty;

    /// <summary>
    /// Amount charged.
    /// </summary>
    public decimal AmountCharged { get; set; }

    /// <summary>
    /// Amount paid (deposit).
    /// </summary>
    public decimal AmountPaid { get; set; }

    /// <summary>
    /// Balance due.
    /// </summary>
    public decimal BalanceDue { get; set; }

    /// <summary>
    /// Expected delivery date.
    /// </summary>
    public DateTime? DeliveryDate { get; set; }

    /// <summary>
    /// Current order status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Time order was received.
    /// </summary>
    public DateTime ReceivedAt { get; set; }
}
