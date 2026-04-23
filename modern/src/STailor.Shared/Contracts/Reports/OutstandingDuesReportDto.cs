namespace STailor.Shared.Contracts.Reports;

/// <summary>
/// Outstanding dues report DTO for API transfer.
/// </summary>
public class OutstandingDuesReportDto
{
    public DateTime GeneratedAt { get; set; }
    public int TotalOrdersWithDues { get; set; }
    public decimal TotalOutstandingAmount { get; set; }
    public decimal TotalCollectedAmount { get; set; }
    public decimal CollectionRatePercent { get; set; }
    public int OverdueCount { get; set; }
    public decimal OverdueAmount { get; set; }
    public List<DuesByStatusSummaryDto> ByStatus { get; set; } = new();
    public List<OutstandingDueItemDto> Orders { get; set; } = new();
}

public class DuesByStatusSummaryDto
{
    public string Status { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public decimal OutstandingAmount { get; set; }
}

public class OutstandingDueItemDto
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal AmountCharged { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal BalanceDue { get; set; }
    public DateTime? DueDate { get; set; }
    public int DaysOverdue { get; set; }
    public bool IsOverdue { get; set; }
    public DateTime? LastPaymentDate { get; set; }
    public DateTime ReceivedAt { get; set; }
}
