using FluentValidation;
using STailor.Core.Application.Commands;

namespace STailor.Modules.Core.Validation;

public sealed class TransitionOrderStatusCommandValidator : AbstractValidator<TransitionOrderStatusCommand>
{
    public TransitionOrderStatusCommandValidator()
    {
        RuleFor(command => command.OrderId)
            .NotEmpty();
    }
}
