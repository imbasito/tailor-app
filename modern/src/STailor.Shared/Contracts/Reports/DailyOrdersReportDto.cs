namespace STailor.Shared.Contracts.Reports;

/// <summary>
/// Daily orders report DTO for API transfer.
/// </summary>
public class DailyOrdersReportDto
{
    public DateTime ReportDate { get; set; }
    public int TotalOrdersReceived { get; set; }
    public decimal TotalAmountCharged { get; set; }
    public decimal TotalAmountPaid { get; set; }
    public List<GarmentTypeSummaryDto> ByGarmentType { get; set; } = new();
    public List<DailyOrderItemDto> Orders { get; set; } = new();
}

public class GarmentTypeSummaryDto
{
    public string GarmentType { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal TotalAmount { get; set; }
}

public class DailyOrderItemDto
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string GarmentType { get; set; } = string.Empty;
    public decimal AmountCharged { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceDue { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
}
