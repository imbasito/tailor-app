using FluentValidation;
using STailor.Core.Application.Abstractions.Services;

namespace STailor.Modules.Core.Validation;

/// <summary>
/// Validator for OutstandingDuesFilter.
/// </summary>
public class OutstandingDuesFilterValidator : AbstractValidator<OutstandingDuesFilter>
{
    public OutstandingDuesFilterValidator()
    {
        RuleFor(x => x.MinBalanceDue)
            .GreaterThanOrEqualTo(0).When(x => x.MinBalanceDue.HasValue)
            .WithMessage("Minimum balance due must be non-negative.");

        RuleFor(x => x.MaxBalanceDue)
            .GreaterThanOrEqualTo(0).When(x => x.MaxBalanceDue.HasValue)
            .WithMessage("Maximum balance due must be non-negative.");

        RuleFor(x => x)
            .Must(filter => !filter.MinBalanceDue.HasValue || !filter.MaxBalanceDue.HasValue || filter.MinBalanceDue <= filter.MaxBalanceDue)
            .WithMessage("Minimum balance due cannot exceed maximum balance due.");

        RuleFor(x => x.OrderBy)
            .Must(orderBy => orderBy == null || new[] { "BalanceDesc", "DueDateAsc", "CustomerName" }.Contains(orderBy))
            .WithMessage("OrderBy must be one of: BalanceDesc, DueDateAsc, CustomerName");
    }
}
