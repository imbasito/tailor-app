using System.Globalization;
using STailor.Core.Application.Abstractions.Services;
using STailor.Core.Application.Commands;
using STailor.Core.Application.Migration;
using STailor.Core.Domain.Exceptions;

namespace STailor.Modules.Core.Tests.Fakes;

internal sealed class FakeLegacyMigrationMapper : ILegacyMigrationMapper
{
    public HashSet<int> CustomerIdsToFail { get; } = [];

    public HashSet<int> OrderIdsToFail { get; } = [];

    public CreateCustomerCommand MapCustomer(LegacyCustomerRecord record)
    {
        if (CustomerIdsToFail.Contains(record.LegacyId))
        {
            throw new DomainRuleViolationException($"Customer {record.LegacyId} failed to map.");
        }

        return new CreateCustomerCommand(
            FullName: record.FullName ?? $"Legacy Customer {record.LegacyId}",
            PhoneNumber: record.Phone ?? $"LEGACY-{record.LegacyId}",
            City: record.City ?? "Unknown",
            Notes: record.Comment);
    }

    public CreateOrderCommand MapOrder(LegacyOrderRecord record, Guid mappedCustomerId)
    {
        if (OrderIdsToFail.Contains(record.LegacyId))
        {
            throw new DomainRuleViolationException($"Order {record.LegacyId} failed to map.");
        }

        if (!decimal.TryParse(record.AmountCharged, NumberStyles.Number, CultureInfo.InvariantCulture, out var charged))
        {
            throw new DomainRuleViolationException($"Invalid amount charged on order {record.LegacyId}.");
        }

        if (!decimal.TryParse(record.AmountPaid, NumberStyles.Number, CultureInfo.InvariantCulture, out var paid))
        {
            throw new DomainRuleViolationException($"Invalid amount paid on order {record.LegacyId}.");
        }

        var due = DateTimeOffset.UtcNow.Date.AddDays(7);

        return new CreateOrderCommand(
            CustomerId: mappedCustomerId,
            GarmentType: string.IsNullOrWhiteSpace(record.Description) ? "General" : record.Description.Trim(),
            OverrideMeasurements: null,
            AmountCharged: charged,
            InitialDeposit: paid,
            DueAtUtc: due);
    }
}
