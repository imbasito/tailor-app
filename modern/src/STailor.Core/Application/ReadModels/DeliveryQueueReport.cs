namespace STailor.Core.Application.ReadModels;

/// <summary>
/// Delivery queue report showing orders ready or expected for delivery.
/// </summary>
public class DeliveryQueueReport
{
    /// <summary>
    /// Report date range start.
    /// </summary>
    public DateTime FromDate { get; set; }

    /// <summary>
    /// Report date range end.
    /// </summary>
    public DateTime ToDate { get; set; }

    /// <summary>
    /// Total orders in delivery queue.
    /// </summary>
    public int TotalOrders { get; set; }

    /// <summary>
    /// Orders ready for delivery.
    /// </summary>
    public int ReadyForDeliveryCount { get; set; }

    /// <summary>
    /// Orders still in progress.
    /// </summary>
    public int InProgressCount { get; set; }

    /// <summary>
    /// Total balance due for queue items.
    /// </summary>
    public decimal TotalBalanceDue { get; set; }

    /// <summary>
    /// Orders grouped by delivery date.
    /// </summary>
    public List<DeliveryDateGroup> ByDeliveryDate { get; set; } = new();
}

/// <summary>
/// Orders grouped by a specific delivery date.
/// </summary>
public class DeliveryDateGroup
{
    /// <summary>
    /// Delivery date.
    /// </summary>
    public DateTime DeliveryDate { get; set; }

    /// <summary>
    /// Number of orders.
    /// </summary>
    public int OrderCount { get; set; }

    /// <summary>
    /// Total balance due for this date.
    /// </summary>
    public decimal TotalBalanceDue { get; set; }

    /// <summary>
    /// Orders for this date.
    /// </summary>
    public List<DeliveryQueueItem> Orders { get; set; } = new();
}

/// <summary>
/// Individual item in delivery queue.
/// </summary>
public class DeliveryQueueItem
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
    /// Customer name.
    /// </summary>
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>
    /// Customer phone.
    /// </summary>
    public string CustomerPhone { get; set; } = string.Empty;

    /// <summary>
    /// Garment type.
    /// </summary>
    public string GarmentType { get; set; } = string.Empty;

    /// <summary>
    /// Order status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Whether order is ready for delivery.
    /// </summary>
    public bool IsReady { get; set; }

    /// <summary>
    /// Amount charged.
    /// </summary>
    public decimal AmountCharged { get; set; }

    /// <summary>
    /// Total paid.
    /// </summary>
    public decimal TotalPaid { get; set; }

    /// <summary>
    /// Balance due at delivery.
    /// </summary>
    public decimal BalanceDue { get; set; }

    /// <summary>
    /// Delivery date.
    /// </summary>
    public DateTime DeliveryDate { get; set; }

    /// <summary>
    /// Whether balance is settled.
    /// </summary>
    public bool IsBalanceSettled { get; set; }

    /// <summary>
    /// Days until/since delivery date.
    /// </summary>
    public int DaysUntilDelivery { get; set; }
}
