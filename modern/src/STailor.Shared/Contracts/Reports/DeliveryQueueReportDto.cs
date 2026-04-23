namespace STailor.Shared.Contracts.Reports;

/// <summary>
/// Delivery queue report DTO for API transfer.
/// </summary>
public class DeliveryQueueReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int TotalOrders { get; set; }
    public int ReadyForDeliveryCount { get; set; }
    public int InProgressCount { get; set; }
    public decimal TotalBalanceDue { get; set; }
    public List<DeliveryDateGroupDto> ByDeliveryDate { get; set; } = new();
}

public class DeliveryDateGroupDto
{
    public DateTime DeliveryDate { get; set; }
    public int OrderCount { get; set; }
    public decimal TotalBalanceDue { get; set; }
    public List<DeliveryQueueItemDto> Orders { get; set; } = new();
}

public class DeliveryQueueItemDto
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string GarmentType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsReady { get; set; }
    public decimal AmountCharged { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal BalanceDue { get; set; }
    public DateTime DeliveryDate { get; set; }
    public bool IsBalanceSettled { get; set; }
    public int DaysUntilDelivery { get; set; }
}
