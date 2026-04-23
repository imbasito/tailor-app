namespace STailor.Shared.Contracts.Customers;

public sealed record CustomerWorkspaceDetailDto(
    Guid CustomerId,
    string FullName,
    string PhoneNumber,
    string City,
    string? Notes,
    string BaselineMeasurementsJson,
    decimal OutstandingBalance,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<CustomerWorkspaceOrderDto> RecentOrders);
