using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using STailor.Core.Domain.Entities;
using STailor.Core.Domain.Enums;

namespace STailor.Infrastructure.Persistence;

public static class DevelopmentSampleDataSeeder
{
    public const int DemoCustomerCount = 1000;

    private const string SeedActor = "seed";
    private const string DemoCodePrefix = "Demo PK-";

    private static readonly string[] FirstNames =
    [
        "Ayesha", "Ahmed", "Fatima", "Bilal", "Sana", "Hassan", "Maham", "Usman",
        "Zainab", "Danish", "Iqra", "Hamza", "Maryam", "Saad", "Noor", "Fahad",
        "Hira", "Ali", "Mehwish", "Omer", "Nimra", "Shahzaib", "Rabia", "Imran",
    ];

    private static readonly string[] LastNames =
    [
        "Khan", "Ahmed", "Malik", "Sheikh", "Raza", "Qureshi", "Butt", "Chaudhry",
        "Siddiqui", "Awan", "Mirza", "Javed", "Iqbal", "Farooq", "Nawaz", "Akhtar",
    ];

    private static readonly string[] Cities =
    [
        "Karachi", "Lahore", "Islamabad", "Rawalpindi", "Faisalabad", "Multan",
        "Peshawar", "Quetta", "Hyderabad", "Sialkot", "Gujranwala", "Bahawalpur",
    ];

    private static readonly string[] Garments =
    [
        "Shalwar Kameez", "Suit", "Shirt", "Trouser", "Waistcoat", "Sherwani",
        "Kurta", "Coat", "Blazer", "Abaya",
    ];

    private static readonly string[] Notes =
    [
        "Prefers WhatsApp reminders before pickup.",
        "Usually pays advance in cash.",
        "Needs careful shoulder fitting.",
        "Repeat client with regular seasonal orders.",
        "Prefers simple finishing and quick delivery.",
        "Ask before changing collar or cuff style.",
    ];

    private static readonly string[] PaymentNotes =
    [
        "Cash advance", "JazzCash", "Easypaisa", "Bank transfer", "Card payment", "Balance collection",
    ];

    public static async Task SeedAsync(LocalTailorDbContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var existingSeedPhones = await context.CustomerProfiles
            .Where(customer => customer.CreatedBy == SeedActor)
            .Select(customer => customer.PhoneNumber)
            .ToListAsync(cancellationToken);

        var existingPhoneSet = new HashSet<string>(existingSeedPhones, StringComparer.OrdinalIgnoreCase);
        var customers = new List<CustomerProfile>();
        var orders = new List<Order>();
        var now = new DateTimeOffset(2026, 4, 22, 9, 0, 0, TimeSpan.Zero);

        for (var index = 1; index <= DemoCustomerCount; index++)
        {
            var phone = BuildPakistaniPhoneNumber(index);
            if (existingPhoneSet.Contains(phone))
            {
                continue;
            }

            var garmentType = Garments[(index - 1) % Garments.Length];
            var receivedAtUtc = now.AddDays(-(index % 45)).AddHours(index % 9);
            var dueAtUtc = receivedAtUtc.AddDays(2 + (index % 14));
            var measurementsJson = BuildMeasurementJson(garmentType, index);
            var customer = CreateCustomer(index, phone, garmentType, measurementsJson, receivedAtUtc);
            var order = CreateOrder(index, customer, garmentType, measurementsJson, receivedAtUtc, dueAtUtc, now);

            customers.Add(customer);
            orders.Add(order);
        }

        if (customers.Count == 0)
        {
            return;
        }

        await context.CustomerProfiles.AddRangeAsync(customers, cancellationToken);
        await context.Orders.AddRangeAsync(orders, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static CustomerProfile CreateCustomer(
        int index,
        string phoneNumber,
        string garmentType,
        string measurementsJson,
        DateTimeOffset createdAtUtc)
    {
        var fullName = $"{FirstNames[(index - 1) % FirstNames.Length]} {LastNames[(index - 1) % LastNames.Length]}";
        var city = Cities[(index - 1) % Cities.Length];
        var notes = $"{DemoCodePrefix}{index:D4}. {Notes[(index - 1) % Notes.Length]}";
        var customer = new CustomerProfile(fullName, phoneNumber, city, notes);

        customer.SetBaselineMeasurements(BuildBaselineMeasurementJson(garmentType, measurementsJson));
        customer.StampCreated(createdAtUtc.AddDays(-2), SeedActor);
        customer.StampUpdated(createdAtUtc, SeedActor);
        return customer;
    }

    private static Order CreateOrder(
        int index,
        CustomerProfile customer,
        string garmentType,
        string measurementsJson,
        DateTimeOffset receivedAtUtc,
        DateTimeOffset dueAtUtc,
        DateTimeOffset now)
    {
        var amountCharged = 1200m + ((index % 18) * 250m);
        var order = new Order(customer.Id, garmentType, measurementsJson, amountCharged, receivedAtUtc, dueAtUtc);

        order.StampCreated(receivedAtUtc, SeedActor);
        ApplyDemoPayments(order, index, receivedAtUtc);
        ApplyDemoStatus(order, index, receivedAtUtc);
        order.StampUpdated(now.AddMinutes(-(index % 720)), SeedActor);
        StampPayments(order, now);

        return order;
    }

    private static void ApplyDemoStatus(Order order, int index, DateTimeOffset receivedAtUtc)
    {
        switch (index % 6)
        {
            case 0:
                return;
            case 1:
                order.TransitionTo(OrderStatus.InProgress);
                return;
            case 2:
                order.TransitionTo(OrderStatus.InProgress);
                order.ScheduleTrial(receivedAtUtc.AddDays(2), "Scheduled");
                order.TransitionTo(OrderStatus.TrialFitting);
                return;
            case 3:
                order.TransitionTo(OrderStatus.InProgress);
                order.ScheduleTrial(receivedAtUtc.AddDays(3), "Completed");
                order.TransitionTo(OrderStatus.TrialFitting);
                order.TransitionTo(OrderStatus.Rework);
                return;
            case 4:
                order.TransitionTo(OrderStatus.InProgress);
                order.ScheduleTrial(receivedAtUtc.AddDays(2), "Completed");
                order.TransitionTo(OrderStatus.TrialFitting);
                order.TransitionTo(OrderStatus.Rework);
                order.TransitionTo(OrderStatus.Ready);
                return;
            default:
                order.TransitionTo(OrderStatus.InProgress);
                order.ScheduleTrial(receivedAtUtc.AddDays(2), "Completed");
                order.TransitionTo(OrderStatus.TrialFitting);
                order.TransitionTo(OrderStatus.Rework);
                order.TransitionTo(OrderStatus.Ready);
                order.TransitionTo(OrderStatus.Delivered);
                return;
        }
    }

    private static void ApplyDemoPayments(Order order, int index, DateTimeOffset receivedAtUtc)
    {
        var pattern = index % 5;
        if (pattern == 0)
        {
            return;
        }

        var deposit = decimal.Round(order.AmountCharged * (pattern switch
        {
            1 => 0.25m,
            2 => 0.40m,
            3 => 0.55m,
            _ => 0.70m,
        }), 2, MidpointRounding.AwayFromZero);

        order.ApplyPayment(deposit, receivedAtUtc.AddHours(2), PaymentNotes[index % PaymentNotes.Length]);

        if (pattern == 4)
        {
            order.ApplyPayment(order.BalanceDue, receivedAtUtc.AddDays(1), PaymentNotes[(index + 2) % PaymentNotes.Length]);
        }
    }

    private static string BuildPakistaniPhoneNumber(int index)
    {
        var networkCodes = new[] { "300", "301", "302", "303", "304", "311", "312", "313", "321", "322", "333", "345" };
        var networkCode = networkCodes[(index - 1) % networkCodes.Length];
        return $"+92{networkCode}{index:0000000}";
    }

    private static string BuildMeasurementJson(string garmentType, int index)
    {
        var offset = index % 9;
        var measurements = garmentType switch
        {
            "Shalwar Kameez" => new Dictionary<string, decimal>
            {
                ["Chest"] = 38m + offset,
                ["Length"] = 39m + offset,
                ["Sleeve"] = 23m + (offset / 2m),
                ["Shalwar Length"] = 38m + offset,
                ["Bottom"] = 15m + (offset / 3m),
            },
            "Suit" or "Coat" or "Blazer" => new Dictionary<string, decimal>
            {
                ["Chest"] = 39m + offset,
                ["Waist"] = 33m + offset,
                ["Shoulder"] = 17m + (offset / 3m),
                ["Sleeve"] = 24m + (offset / 2m),
                ["Length"] = 30m + offset,
            },
            "Shirt" or "Kurta" => new Dictionary<string, decimal>
            {
                ["Neck"] = 15m + (offset / 4m),
                ["Chest"] = 38m + offset,
                ["Sleeve"] = 23m + (offset / 2m),
                ["Length"] = 29m + offset,
            },
            "Trouser" => new Dictionary<string, decimal>
            {
                ["Waist"] = 32m + offset,
                ["Hip"] = 38m + offset,
                ["Length"] = 39m + offset,
                ["Bottom"] = 14m + (offset / 3m),
            },
            "Waistcoat" => new Dictionary<string, decimal>
            {
                ["Chest"] = 38m + offset,
                ["Waist"] = 33m + offset,
                ["Shoulder"] = 16m + (offset / 3m),
                ["Length"] = 25m + offset,
            },
            "Sherwani" => new Dictionary<string, decimal>
            {
                ["Chest"] = 40m + offset,
                ["Waist"] = 35m + offset,
                ["Shoulder"] = 18m + (offset / 3m),
                ["Sleeve"] = 25m + (offset / 2m),
                ["Length"] = 42m + offset,
            },
            _ => new Dictionary<string, decimal>
            {
                ["Chest"] = 38m + offset,
                ["Waist"] = 34m + offset,
                ["Length"] = 45m + offset,
            },
        };

        return JsonSerializer.Serialize(measurements);
    }

    private static string BuildBaselineMeasurementJson(string garmentType, string measurementsJson)
    {
        var measurements = JsonSerializer.Deserialize<Dictionary<string, decimal>>(measurementsJson) ?? [];
        return JsonSerializer.Serialize(measurements.ToDictionary(
            pair => $"{garmentType}:{pair.Key}",
            pair => pair.Value,
            StringComparer.OrdinalIgnoreCase));
    }

    private static void StampPayments(Order order, DateTimeOffset now)
    {
        foreach (var payment in order.Payments)
        {
            payment.StampCreated(payment.PaidAtUtc, SeedActor);
            payment.StampUpdated(now, SeedActor);
        }
    }
}
