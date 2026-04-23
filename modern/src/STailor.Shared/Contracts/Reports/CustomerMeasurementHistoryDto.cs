namespace STailor.Shared.Contracts.Reports;

/// <summary>
/// Customer measurement history report DTO.
/// </summary>
public class CustomerMeasurementHistoryDto
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? CustomerNotes { get; set; }
    public MeasurementSnapshotDto? CurrentBaseline { get; set; }
    public List<OrderMeasurementSnapshotDto> OrderHistory { get; set; } = new();
    public int TotalOrders { get; set; }
    public DateTime? LastOrderDate { get; set; }
}

public class MeasurementSnapshotDto
{
    public DateTime LastUpdated { get; set; }
    public string GarmentType { get; set; } = string.Empty;
    public Dictionary<string, decimal> Measurements { get; set; } = new();
    public string? Notes { get; set; }
}

public class OrderMeasurementSnapshotDto
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public string GarmentType { get; set; } = string.Empty;
    public Dictionary<string, decimal> Measurements { get; set; } = new();
    public string? Notes { get; set; }
    public bool DeviatedFromBaseline { get; set; }
}
