using STailor.Core.Application.Migration;
using STailor.Core.Domain.Exceptions;
using STailor.Infrastructure.Services;

namespace STailor.Infrastructure.Tests.Services;

public sealed class LegacyMigrationMapperTests
{
    [Fact]
    public void MapCustomer_WhenFieldsMissing_UsesFallbackValues()
    {
        var mapper = new LegacyMigrationMapper();

        var command = mapper.MapCustomer(new LegacyCustomerRecord(42, null, null, null, null));

        Assert.Equal("Legacy Customer 42", command.FullName);
        Assert.Equal("LEGACY-42", command.PhoneNumber);
        Assert.Equal("Unknown", command.City);
        Assert.Contains("legacy customer id 42", command.Notes!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MapOrder_ParsesLegacyValuesAndClampsPaidAmount()
    {
        var mapper = new LegacyMigrationMapper();
        var customerId = Guid.NewGuid();

        var command = mapper.MapOrder(
            new LegacyOrderRecord(
                LegacyId: 9,
                LegacyCustomerId: 7,
                Description: "Black Suit order",
                RecievedOn: "2026-04-10",
                AmountCharged: "1200.50",
                AmountPaid: "1500",
                CollectingOn: "2026-04-14"),
            customerId);

        Assert.Equal(customerId, command.CustomerId);
        Assert.Equal("Suit", command.GarmentType);
        Assert.Equal(1200.50m, command.AmountCharged);
        Assert.Equal(1200.50m, command.InitialDeposit);
        Assert.Equal(new DateTimeOffset(2026, 4, 14, 0, 0, 0, TimeSpan.Zero), command.DueAtUtc);
    }

    [Fact]
    public void MapOrder_InvalidMoney_ThrowsDomainRuleViolationException()
    {
        var mapper = new LegacyMigrationMapper();

        Assert.Throws<DomainRuleViolationException>(() =>
            mapper.MapOrder(
                new LegacyOrderRecord(
                    LegacyId: 10,
                    LegacyCustomerId: 7,
                    Description: "Shirt",
                    RecievedOn: "2026-04-10",
                    AmountCharged: "abc",
                    AmountPaid: "0",
                    CollectingOn: "2026-04-20"),
                Guid.NewGuid()));
    }

    [Fact]
    public void MapOrder_MissingCollectingDate_UsesReceivedPlusSevenDays()
    {
        var mapper = new LegacyMigrationMapper();

        var command = mapper.MapOrder(
            new LegacyOrderRecord(
                LegacyId: 11,
                LegacyCustomerId: 7,
                Description: "Trouser",
                RecievedOn: "2026-04-10",
                AmountCharged: "500",
                AmountPaid: "100",
                CollectingOn: null),
            Guid.NewGuid());

        Assert.Equal(new DateTimeOffset(2026, 4, 17, 0, 0, 0, TimeSpan.Zero), command.DueAtUtc);
        Assert.Equal("Trouser", command.GarmentType);
    }
}
