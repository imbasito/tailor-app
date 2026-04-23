using FluentValidation;
using STailor.Core.Application.Commands;

namespace STailor.Modules.Core.Validation;

public sealed class AddPaymentCommandValidator : AbstractValidator<AddPaymentCommand>
{
    public AddPaymentCommandValidator()
    {
        RuleFor(command => command.OrderId)
            .NotEmpty();

        RuleFor(command => command.Amount)
            .GreaterThan(0);
    }
}
