using STailor.UI.Rcl.Models;
using STailor.UI.Rcl.Services;
using STailor.Shared.Contracts.Orders;
using STailor.Shared.Contracts.Reports;

namespace STailor.UI.Rcl.Tests.Services;

public sealed class WhatsAppPrefillLinkBuilderTests
{
    [Fact]
    public void BuildFromOrderResult_ForStatusUpdate_UsesWhatsAppRouteAndEncodesFields()
    {
        var result = OrderWizardSubmissionResult.Success(
            customerId: Guid.Parse("32af6b80-8b8f-493d-b506-89dbf4f915f2"),
            orderId: Guid.Parse("a4f15f89-34e5-4668-8728-a0c205aa6d66"),
            finalStatus: "Ready",
            customerName: "Amina Noor",
            phoneNumber: "+251900000001",
            dueAtUtc: new DateTimeOffset(2026, 4, 25, 0, 0, 0, TimeSpan.Zero),
            balanceDue: 2000m);

        var link = WhatsAppPrefillLinkBuilder.BuildFromOrderResult(result, useDueBalanceTemplate: false);

        Assert.StartsWith("/communications/whatsapp?", link, StringComparison.Ordinal);
        Assert.Contains("orderId=a4f15f89-34e5-4668-8728-a0c205aa6d66", link, StringComparison.Ordinal);
        Assert.DoesNotContain("phone=", link, StringComparison.Ordinal);
        Assert.DoesNotContain("customerName=", link, StringComparison.Ordinal);
        Assert.DoesNotContain("status=", link, StringComparison.Ordinal);
        Assert.DoesNotContain("dueAtUtc=", link, StringComparison.Ordinal);
        Assert.DoesNotContain("balanceDue=", link, StringComparison.Ordinal);
        Assert.DoesNotContain("template=balance", link, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildFromOrderResult_ForDueBalanceReminder_UsesReminderRouteAndTemplateFlag()
    {
        var result = OrderWizardSubmissionResult.Success(
            customerId: Guid.Parse("85b8fd42-76e7-4cc8-8e28-2f0e729495be"),
            orderId: Guid.Parse("e0871f90-8c47-4d65-b9af-6cb4df5db5be"),
            finalStatus: "Rework",
            customerName: "Samir Ali",
            phoneNumber: "+251900000002",
            dueAtUtc: new DateTimeOffset(2026, 4, 28, 0, 0, 0, TimeSpan.Zero),
            balanceDue: 3150.75m);

        var link = WhatsAppPrefillLinkBuilder.BuildFromOrderResult(result, useDueBalanceTemplate: true);

        Assert.StartsWith("/communications/reminder?", link, StringComparison.Ordinal);
        Assert.Contains("template=balance", link, StringComparison.Ordinal);
        Assert.Contains("orderId=e0871f90-8c47-4d65-b9af-6cb4df5db5be", link, StringComparison.Ordinal);
        Assert.DoesNotContain("balanceDue=", link, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildFromReminderCandidate_ForStatusUpdate_UsesWhatsAppRoute()
    {
        var candidate = new OrderReminderDto(
            OrderId: Guid.Parse("9620cdf2-eb2f-4fc5-bf8f-b57664a2f969"),
            CustomerId: Guid.Parse("8775ba5b-64d4-441c-bf00-0296f0900edf"),
            CustomerName: "Muna Ali",
            PhoneNumber: "+251900000003",
            GarmentType: "Shirt",
            Status: "InProgress",
            AmountCharged: 1300m,
            AmountPaid: 300m,
            BalanceDue: 1000m,
            DueAtUtc: new DateTimeOffset(2026, 4, 29, 0, 0, 0, TimeSpan.Zero));

        var link = WhatsAppPrefillLinkBuilder.BuildFromReminderCandidate(candidate, useDueBalanceTemplate: false);

        Assert.StartsWith("/communications/whatsapp?", link, StringComparison.Ordinal);
        Assert.Contains("orderId=9620cdf2-eb2f-4fc5-bf8f-b57664a2f969", link, StringComparison.Ordinal);
        Assert.DoesNotContain("phone=", link, StringComparison.Ordinal);
        Assert.DoesNotContain("customerName=", link, StringComparison.Ordinal);
        Assert.DoesNotContain("status=", link, StringComparison.Ordinal);
        Assert.DoesNotContain("template=balance", link, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildFromWorklistItem_ForDueBalanceReminder_UsesReminderRoute()
    {
        var item = new OrderWorklistItemDto(
            OrderId: Guid.Parse("6c2b0f99-7f90-4883-8448-c3c16b0f95f4"),
            CustomerId: Guid.Parse("b1f8f3cf-6f08-45f2-9ec2-7442ab0016c7"),
            CustomerName: "Khalid Noor",
            PhoneNumber: "+251900000004",
            City: "Adama",
            GarmentType: "Coat",
            Status: "Ready",
            AmountCharged: 3500m,
            AmountPaid: 1000m,
            BalanceDue: 2500m,
            ReceivedAtUtc: new DateTimeOffset(2026, 4, 20, 0, 0, 0, TimeSpan.Zero),
            DueAtUtc: new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero));

        var link = WhatsAppPrefillLinkBuilder.BuildFromWorklistItem(item, useDueBalanceTemplate: true);

        Assert.StartsWith("/communications/reminder?", link, StringComparison.Ordinal);
        Assert.Contains("orderId=6c2b0f99-7f90-4883-8448-c3c16b0f95f4", link, StringComparison.Ordinal);
        Assert.Contains("template=balance", link, StringComparison.Ordinal);
        Assert.DoesNotContain("phone=", link, StringComparison.Ordinal);
        Assert.DoesNotContain("customerName=", link, StringComparison.Ordinal);
        Assert.DoesNotContain("balanceDue=", link, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildFromWorklistGroup_ForStatusUpdate_UsesWhatsAppRouteAndIncludesMessage()
    {
        var group = new OrdersBoardWorklistGroup(
            Kind: OrdersBoardWorklistGroupKind.Overdue,
            Title: "Overdue",
            Items:
            [
                new OrderWorklistItemDto(
                    OrderId: Guid.Parse("7cc5f11c-c958-4933-a8cc-0eb719f0d1c0"),
                    CustomerId: Guid.Parse("4a9392f4-9f76-41f8-afd2-f406ca5a5412"),
                    CustomerName: "Amina Noor",
                    PhoneNumber: "+251900000001",
                    City: "Harar",
                    GarmentType: "Suit",
                    Status: "InProgress",
                    AmountCharged: 300m,
                    AmountPaid: 50m,
                    BalanceDue: 250m,
                    ReceivedAtUtc: new DateTimeOffset(2026, 4, 18, 0, 0, 0, TimeSpan.Zero),
                    DueAtUtc: new DateTimeOffset(2026, 4, 20, 0, 0, 0, TimeSpan.Zero)),
            ]);

        var link = WhatsAppPrefillLinkBuilder.BuildFromWorklistGroup(group, useDueBalanceTemplate: false);

        Assert.StartsWith("/communications/whatsapp?", link, StringComparison.Ordinal);
        Assert.Contains("customerName=Group%3A%20Overdue", link, StringComparison.Ordinal);
        Assert.Contains("status=Overdue", link, StringComparison.Ordinal);
        Assert.Contains("balanceDue=250", link, StringComparison.Ordinal);
        Assert.Contains("message=Selam%2C%20SINYX%20Tailor%20Management%20status%20updates%20for%20Overdue.", link, StringComparison.Ordinal);
        Assert.DoesNotContain("template=balance", link, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildFromWorklistGroup_ForDueBalanceReminder_UsesReminderRouteAndTemplateFlag()
    {
        var group = new OrdersBoardWorklistGroup(
            Kind: OrdersBoardWorklistGroupKind.ReadyWithBalance,
            Title: "Ready With Balance",
            Items:
            [
                new OrderWorklistItemDto(
                    OrderId: Guid.Parse("f6b9edc8-a53d-4af7-8d95-740fc5cc0c1f"),
                    CustomerId: Guid.Parse("e58fea29-7232-40e4-b8ea-9ca20b6bb3d0"),
                    CustomerName: "Samir Ali",
                    PhoneNumber: "+251900000002",
                    City: "Dire Dawa",
                    GarmentType: "Shirt",
                    Status: "Ready",
                    AmountCharged: 500m,
                    AmountPaid: 100m,
                    BalanceDue: 400m,
                    ReceivedAtUtc: new DateTimeOffset(2026, 4, 18, 0, 0, 0, TimeSpan.Zero),
                    DueAtUtc: new DateTimeOffset(2026, 4, 23, 0, 0, 0, TimeSpan.Zero)),
            ]);

        var link = WhatsAppPrefillLinkBuilder.BuildFromWorklistGroup(group, useDueBalanceTemplate: true);

        Assert.StartsWith("/communications/reminder?", link, StringComparison.Ordinal);
        Assert.Contains("template=balance", link, StringComparison.Ordinal);
        Assert.Contains("status=Ready%20With%20Balance", link, StringComparison.Ordinal);
        Assert.Contains("balanceDue=400", link, StringComparison.Ordinal);
        Assert.Contains("message=Selam%2C%20SINYX%20Tailor%20Management%20balance%20reminders%20for%20Ready%20With%20Balance.", link, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildFromRetryCandidatesGroup_UsesWhatsAppRouteAndRetryableOnlyMessage()
    {
        var candidates =
            new List<OrdersBoardBulkAdvanceRetryCandidate>
            {
                new(Guid.Parse("f6b9edc8-a53d-4af7-8d95-740fc5cc0c1f"), "Zara", "Ready"),
                new(Guid.Parse("7cc5f11c-c958-4933-a8cc-0eb719f0d1c0"), "Noor", "InProgress"),
                new(Guid.Parse("a4f15f89-34e5-4668-8728-a0c205aa6d66"), "Amina", "InProgress"),
                new(Guid.Parse("6c2b0f99-7f90-4883-8448-c3c16b0f95f4"), "Bilal", "TrialFitting"),
            };

        var link = WhatsAppPrefillLinkBuilder.BuildFromRetryCandidatesGroup("Overdue", candidates);

        Assert.StartsWith("/communications/whatsapp?", link, StringComparison.Ordinal);
        Assert.Contains("customerName=Retryable%3A%20Overdue", link, StringComparison.Ordinal);
        Assert.Contains("status=Retryable", link, StringComparison.Ordinal);
        Assert.DoesNotContain("template=balance", link, StringComparison.Ordinal);

        var message = GetQueryValue(link, "message");
        Assert.StartsWith("Selam, SINYX Tailor Management retryable updates for Overdue.", message, StringComparison.Ordinal);
        Assert.Contains("- Amina", message, StringComparison.Ordinal);
        Assert.Contains("- Noor", message, StringComparison.Ordinal);
        Assert.Contains("- Bilal", message, StringComparison.Ordinal);
        Assert.Contains("- Zara", message, StringComparison.Ordinal);
        Assert.Contains("Trial/Fitting", message, StringComparison.Ordinal);

        var aminaIndex = message.IndexOf("- Amina", StringComparison.Ordinal);
        var noorIndex = message.IndexOf("- Noor", StringComparison.Ordinal);
        var bilalIndex = message.IndexOf("- Bilal", StringComparison.Ordinal);
        var zaraIndex = message.IndexOf("- Zara", StringComparison.Ordinal);

        Assert.True(aminaIndex >= 0);
        Assert.True(noorIndex >= 0);
        Assert.True(bilalIndex >= 0);
        Assert.True(zaraIndex >= 0);
        Assert.True(aminaIndex < noorIndex);
        Assert.True(noorIndex < bilalIndex);
        Assert.True(bilalIndex < zaraIndex);
    }

    [Fact]
    public void BuildFromOutstandingDueItem_UsesReminderRouteAndEncodesFields()
    {
        var item = new OutstandingDueItemDto
        {
            OrderId = Guid.Parse("7cc5f11c-c958-4933-a8cc-0eb719f0d1c0"),
            OrderNumber = "ORD-1024",
            CustomerId = Guid.Parse("4a9392f4-9f76-41f8-afd2-f406ca5a5412"),
            CustomerName = "Amina Noor",
            CustomerPhone = "+251900000001",
            Status = "Ready",
            BalanceDue = 250m,
            DueDate = new DateTime(2026, 4, 20),
        };

        var link = WhatsAppPrefillLinkBuilder.BuildFromOutstandingDueItem(item);

        Assert.StartsWith("/communications/reminder?", link, StringComparison.Ordinal);
        Assert.Contains("orderId=7cc5f11c-c958-4933-a8cc-0eb719f0d1c0", link, StringComparison.Ordinal);
        Assert.Contains("template=balance", link, StringComparison.Ordinal);
        Assert.DoesNotContain("phone=", link, StringComparison.Ordinal);
        Assert.DoesNotContain("customerName=", link, StringComparison.Ordinal);
    }

    private static string GetQueryValue(string link, string key)
    {
        var queryStart = link.IndexOf('?', StringComparison.Ordinal);
        if (queryStart < 0 || queryStart == link.Length - 1)
        {
            return string.Empty;
        }

        var queryParts = link[(queryStart + 1)..]
            .Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var queryPart in queryParts)
        {
            var pair = queryPart.Split('=', 2, StringSplitOptions.TrimEntries);
            if (pair.Length == 0)
            {
                continue;
            }

            var currentKey = Uri.UnescapeDataString(pair[0]);
            if (!string.Equals(currentKey, key, StringComparison.Ordinal))
            {
                continue;
            }

            return pair.Length > 1
                ? Uri.UnescapeDataString(pair[1])
                : string.Empty;
        }

        return string.Empty;
    }
}
