using STailor.Core.Common.Entities;
using STailor.Core.Domain.Enums;
using STailor.Core.Domain.Exceptions;

namespace STailor.Core.Domain.Entities;

public class Order : AuditableEntity
{
    private Order()
    {
    }

    public Order(
        Guid customerProfileId,
        string garmentType,
        string measurementSnapshotJson,
        decimal amountCharged,
        DateTimeOffset receivedAtUtc,
        DateTimeOffset dueAtUtc,
        string? photoAttachmentsJson = null)
    {
        if (customerProfileId == Guid.Empty)
        {
            throw new DomainRuleViolationException("Customer id is required.");
        }

        if (string.IsNullOrWhiteSpace(garmentType))
        {
            throw new DomainRuleViolationException("Garment type is required.");
        }

        if (string.IsNullOrWhiteSpace(measurementSnapshotJson))
        {
            throw new DomainRuleViolationException("Order measurement snapshot is required.");
        }

        if (amountCharged <= 0)
        {
            throw new DomainRuleViolationException("Amount charged must be greater than zero.");
        }

        if (dueAtUtc < receivedAtUtc)
        {
            throw new DomainRuleViolationException("Due date cannot be earlier than received date.");
        }

        CustomerProfileId = customerProfileId;
        GarmentType = garmentType.Trim();
        MeasurementSnapshotJson = measurementSnapshotJson;
        PhotoAttachmentsJson = string.IsNullOrWhiteSpace(photoAttachmentsJson)
            ? "[]"
            : photoAttachmentsJson;
        AmountCharged = decimal.Round(amountCharged, 2, MidpointRounding.AwayFromZero);
        ReceivedAtUtc = receivedAtUtc;
        DueAtUtc = dueAtUtc;
        Status = OrderStatus.New;
    }

    public Guid CustomerProfileId { get; private set; }

    public string GarmentType { get; private set; } = string.Empty;

    public string MeasurementSnapshotJson { get; private set; } = "{}";

    public string PhotoAttachmentsJson { get; private set; } = "[]";

    public OrderStatus Status { get; private set; }

    public decimal AmountCharged { get; private set; }

    public decimal AmountPaid { get; private set; }

    public decimal BalanceDue => AmountCharged - AmountPaid;

    public DateTimeOffset ReceivedAtUtc { get; private set; }

    public DateTimeOffset DueAtUtc { get; private set; }

    public DateTimeOffset? TrialScheduledAtUtc { get; private set; }

    public string? TrialScheduleStatus { get; private set; }

    public ICollection<Payment> Payments { get; private set; } = new List<Payment>();

    public Payment ApplyPayment(decimal amount, DateTimeOffset paidAtUtc, string? note = null)
    {
        if (amount <= 0)
        {
            throw new DomainRuleViolationException("Payment amount must be greater than zero.");
        }

        var roundedAmount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);

        if (AmountPaid + roundedAmount > AmountCharged)
        {
            throw new DomainRuleViolationException("Payment exceeds current balance due.");
        }

        var payment = new Payment(Id, roundedAmount, paidAtUtc, note);
        Payments.Add(payment);
        AmountPaid += roundedAmount;
        return payment;
    }

    public void TransitionTo(OrderStatus targetStatus)
    {
        if (!IsTransitionAllowed(Status, targetStatus, out var error))
        {
            throw new DomainRuleViolationException(error ?? $"Invalid order status transition from {Status} to {targetStatus}.");
        }

        Status = targetStatus;
    }

    public void ScheduleTrial(DateTimeOffset trialAtUtc, string scheduleStatus)
    {
        if (string.IsNullOrWhiteSpace(scheduleStatus))
        {
            throw new DomainRuleViolationException("Trial schedule status is required.");
        }

        if (trialAtUtc < ReceivedAtUtc)
        {
            throw new DomainRuleViolationException("Trial schedule time cannot be earlier than order received time.");
        }

        TrialScheduledAtUtc = trialAtUtc;
        TrialScheduleStatus = scheduleStatus.Trim();
    }

    public void SetPhotoAttachments(string attachmentsJson)
    {
        PhotoAttachmentsJson = string.IsNullOrWhiteSpace(attachmentsJson)
            ? "[]"
            : attachmentsJson;
    }

    private static bool IsTransitionAllowed(OrderStatus currentStatus, OrderStatus targetStatus, out string? error)
    {
        error = null;
        // Disallow no-op
        if (currentStatus == targetStatus)
        {
            error = $"Order is already in status {currentStatus}.";
            return false;
        }
        // Only allow moving to the immediate next state
        var next = currentStatus switch
        {
            OrderStatus.New => OrderStatus.InProgress,
            OrderStatus.InProgress => OrderStatus.TrialFitting,
            OrderStatus.TrialFitting => OrderStatus.Rework,
            OrderStatus.Rework => OrderStatus.Ready,
            OrderStatus.Ready => OrderStatus.Delivered,
            _ => (OrderStatus)(-1)
        };
        if (targetStatus != next)
        {
            error = $"Invalid sequential transition: {currentStatus} → {targetStatus}. Must proceed stepwise.";
            return false;
        }
        return true;
    }
}
