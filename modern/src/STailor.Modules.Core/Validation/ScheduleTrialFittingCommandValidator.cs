using FluentValidation;
using STailor.Core.Application.Commands;

namespace STailor.Modules.Core.Validation;

public sealed class ScheduleTrialFittingCommandValidator : AbstractValidator<ScheduleTrialFittingCommand>
{
    public ScheduleTrialFittingCommandValidator()
    {
        RuleFor(command => command.OrderId)
            .NotEmpty();

        RuleFor(command => command.ScheduleStatus)
            .NotEmpty()
            .MaximumLength(32);
    }
}
