namespace STailor.Shared.Contracts.Customers;

public sealed record UpdateCustomerRequest(
    string FullName,
    string PhoneNumber,
    string City,
    string? Notes);
