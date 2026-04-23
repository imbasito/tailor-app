namespace STailor.Core.Application.ReadModels;

/// <summary>
/// Customer measurement history report.
/// </summary>
public class CustomerMeasurementHistoryReport
{
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
    /// Customer address or notes.
    /// </summary>
    public string? CustomerNotes { get; set; }

    /// <summary>
    /// Current baseline measurements (latest profile).
    /// </summary>
    public MeasurementSnapshot? CurrentBaseline { get; set; }

    /// <summary>
    /// Measurement history from orders.
    /// </summary>
    public List<OrderMeasurementSnapshot> OrderHistory { get; set; } = new();

    /// <summary>
    /// Total orders placed.
    /// </summary>
    public int TotalOrders { get; set; }

    /// <summary>
    /// Last order date.
    /// </summary>
    public DateTime? LastOrderDate { get; set; }
}

/// <summary>
/// Measurement snapshot from an order.
/// </summary>
public class OrderMeasurementSnapshot
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
    /// Order date.
    /// </summary>
    public DateTime OrderDate { get; set; }

    /// <summary>
    /// Garment type.
    /// </summary>
    public string GarmentType { get; set; } = string.Empty;

    /// <summary>
    /// Measurements taken at time of order.
    /// </summary>
    public Dictionary<string, decimal> Measurements { get; set; } = new();

    /// <summary>
    /// Measurement notes.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Whether these measurements were different from baseline.
    /// </summary>
    public bool DeviatedFromBaseline { get; set; }
}

/// <summary>
/// Current baseline measurement snapshot.
/// </summary>
public class MeasurementSnapshot
{
    /// <summary>
    /// Date baseline was last updated.
    /// </summary>
    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// Garment type.
    /// </summary>
    public string GarmentType { get; set; } = string.Empty;

    /// <summary>
    /// Measurements dictionary (name -> value).
    /// </summary>
    public Dictionary<string, decimal> Measurements { get; set; } = new();

    /// <summary>
    /// Notes.
    /// </summary>
    public string? Notes { get; set; }
}
