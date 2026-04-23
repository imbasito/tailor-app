namespace STailor.Core.Application.ReadModels;

public sealed record CentralSyncPullResult(
    int CustomersProcessed,
    int CustomersApplied,
    int OrdersProcessed,
    int OrdersApplied,
    int PaymentsProcessed,
    int PaymentsApplied)
{
    public int TotalProcessed => CustomersProcessed + OrdersProcessed + PaymentsProcessed;

    public int TotalApplied => CustomersApplied + OrdersApplied + PaymentsApplied;
}
