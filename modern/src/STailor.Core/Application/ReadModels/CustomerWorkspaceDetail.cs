namespace STailor.Core.Application.ReadModels;

public sealed record CustomerWorkspaceDetail(
    Guid CustomerId,
    string FullName,
    string PhoneNumber,
    string City,
    string? Notes,
    string BaselineMeasurementsJson,
    decimal OutstandingBalance,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<CustomerWorkspaceOrder> RecentOrders);
