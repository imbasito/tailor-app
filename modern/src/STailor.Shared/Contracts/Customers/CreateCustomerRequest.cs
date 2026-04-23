namespace STailor.Shared.Contracts.Customers;

public sealed record CreateCustomerRequest(
    string FullName,
    string PhoneNumber,
    string City,
    string? Notes);
