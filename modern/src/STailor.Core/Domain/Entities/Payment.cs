using STailor.Core.Common.Entities;
using STailor.Core.Domain.Exceptions;

namespace STailor.Core.Domain.Entities;

public class Payment : AuditableEntity
{
    private Payment()
    {
    }

    public Payment(Guid orderId, decimal amount, DateTimeOffset paidAtUtc, string? note = null)
    {
        if (orderId == Guid.Empty)
        {
            throw new DomainRuleViolationException("Order id is required.");
        }

        if (amount <= 0)
        {
            throw new DomainRuleViolationException("Payment amount must be greater than zero.");
        }

        OrderId = orderId;
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        PaidAtUtc = paidAtUtc;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    public Guid OrderId { get; private set; }

    public decimal Amount { get; private set; }

    public DateTimeOffset PaidAtUtc { get; private set; }

    public string? Note { get; private set; }

    public Order? Order { get; private set; }
}
