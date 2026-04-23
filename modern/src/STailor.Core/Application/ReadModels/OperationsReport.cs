namespace STailor.Core.Application.ReadModels;

public sealed class OperationsReport
{
    public DateTime GeneratedAt { get; set; }

    public int TotalOrders { get; set; }

    public int OpenOrders { get; set; }

    public int DeliveredOrders { get; set; }

    public int OverdueOrders { get; set; }

    public decimal TotalCharged { get; set; }

    public decimal TotalPaid { get; set; }

    public decimal TotalBalanceDue { get; set; }

    public decimal CollectionRatePercent { get; set; }

    public List<OperationsStatusSummary> ByStatus { get; set; } = [];

    public List<OperationsGarmentSummary> ByGarment { get; set; } = [];

    public List<OperationsReportOrder> Orders { get; set; } = [];
}

public sealed class OperationsStatusSummary
{
    public string Status { get; set; } = string.Empty;

    public int OrderCount { get; set; }

    public decimal Charged { get; set; }

    public decimal Paid { get; set; }

    public decimal BalanceDue { get; set; }
}

public sealed class OperationsGarmentSummary
{
    public string GarmentType { get; set; } = string.Empty;

    public int OrderCount { get; set; }

    public decimal Charged { get; set; }

    public decimal Paid { get; set; }

    public decimal BalanceDue { get; set; }
}

public sealed class OperationsReportOrder
{
    public Guid OrderId { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public Guid CustomerId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string CustomerPhone { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string GarmentType { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public decimal AmountCharged { get; set; }

    public decimal AmountPaid { get; set; }

    public decimal BalanceDue { get; set; }

    public DateTime ReceivedAt { get; set; }

    public DateTime DueAt { get; set; }

    public int DaysLate { get; set; }

    public bool IsOverdue { get; set; }

    public DateTime? LastPaymentAt { get; set; }
}
