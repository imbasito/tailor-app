namespace STailor.Core.Application.Commands;

public sealed record CreateCustomerCommand(
    string FullName,
    string PhoneNumber,
    string City,
    string? Notes);
