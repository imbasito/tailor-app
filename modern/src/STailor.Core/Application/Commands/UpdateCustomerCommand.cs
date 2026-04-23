namespace STailor.Core.Application.Commands;

public sealed record UpdateCustomerCommand(
    Guid CustomerId,
    string FullName,
    string PhoneNumber,
    string City,
    string? Notes);
