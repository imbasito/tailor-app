using System.Globalization;
using STailor.Core.Application.Abstractions.Services;
using STailor.Core.Application.Commands;
using STailor.Core.Application.Migration;
using STailor.Core.Domain.Exceptions;

namespace STailor.Infrastructure.Services;

public sealed class LegacyMigrationMapper : ILegacyMigrationMapper
{
    public CreateCustomerCommand MapCustomer(LegacyCustomerRecord record)
    {
        var fullName = string.IsNullOrWhiteSpace(record.FullName)
            ? $"Legacy Customer {record.LegacyId}"
            : record.FullName.Trim();

        var phone = string.IsNullOrWhiteSpace(record.Phone)
            ? $"LEGACY-{record.LegacyId}"
            : record.Phone.Trim();

        var city = string.IsNullOrWhiteSpace(record.City)
            ? "Unknown"
            : record.City.Trim();

        var notes = string.IsNullOrWhiteSpace(record.Comment)
            ? $"Migrated from legacy customer id {record.LegacyId}."
            : record.Comment.Trim();

        return new CreateCustomerCommand(fullName, phone, city, notes);
    }

    public CreateOrderCommand MapOrder(LegacyOrderRecord record, Guid mappedCustomerId)
    {
        if (mappedCustomerId == Guid.Empty)
        {
            throw new DomainRuleViolationException("Mapped customer id is required for order migration.");
        }

        var amountCharged = ParseMoney(record.AmountCharged, nameof(record.AmountCharged));
        var amountPaid = ParseMoney(record.AmountPaid, nameof(record.AmountPaid));

        if (amountPaid > amountCharged)
        {
            amountPaid = amountCharged;
        }

        var receivedAt = ParseDate(record.RecievedOn, "RecievedOn", DateTimeOffset.UtcNow.Date);
        var dueAt = ParseDate(record.CollectingOn, "CollectingOn", receivedAt.AddDays(7));

        if (dueAt < receivedAt)
        {
            dueAt = receivedAt.AddDays(7);
        }

        var garmentType = InferGarmentType(record.Description);

        return new CreateOrderCommand(
            mappedCustomerId,
            garmentType,
            OverrideMeasurements: null,
            AmountCharged: amountCharged,
            InitialDeposit: amountPaid,
            DueAtUtc: dueAt);
    }

    private static string InferGarmentType(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return "General";
        }

        var text = description.Trim();
        if (text.Contains("suit", StringComparison.OrdinalIgnoreCase))
        {
            return "Suit";
        }

        if (text.Contains("shirt", StringComparison.OrdinalIgnoreCase))
        {
            return "Shirt";
        }

        if (text.Contains("trouser", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("pant", StringComparison.OrdinalIgnoreCase))
        {
            return "Trouser";
        }

        return "General";
    }

    private static decimal ParseMoney(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0m;
        }

        var normalized = value.Trim().Replace(",", ".", StringComparison.Ordinal);

        if (!decimal.TryParse(
                normalized,
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out var result))
        {
            throw new DomainRuleViolationException($"Invalid monetary value in {fieldName}: '{value}'.");
        }

        if (result < 0)
        {
            throw new DomainRuleViolationException($"Negative monetary value is invalid in {fieldName}.");
        }

        return decimal.Round(result, 2, MidpointRounding.AwayFromZero);
    }

    private static DateTimeOffset ParseDate(string? value, string fieldName, DateTimeOffset fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return parsed;
        }

        if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDateOnly))
        {
            return new DateTimeOffset(parsedDateOnly.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        }

        throw new DomainRuleViolationException($"Invalid date value in {fieldName}: '{value}'.");
    }
}
