using FluentValidation;
using STailor.Core.Application.Commands;

namespace STailor.Modules.Core.Validation;

public sealed class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(command => command.CustomerId)
            .NotEmpty();

        RuleFor(command => command.GarmentType)
            .NotEmpty()
            .MaximumLength(80);

        RuleFor(command => command.AmountCharged)
            .GreaterThan(0);

        RuleFor(command => command.InitialDeposit)
            .GreaterThanOrEqualTo(0);

        RuleFor(command => command)
            .Must(command => command.InitialDeposit <= command.AmountCharged)
            .WithMessage("Initial deposit cannot exceed amount charged.");

        RuleForEach(command => command.PhotoAttachments!)
            .Must(attachment =>
                !string.IsNullOrWhiteSpace(attachment.ResourcePath)
                && !string.IsNullOrWhiteSpace(attachment.FileName))
            .WithMessage("Photo attachments require file name and resource path.")
            .When(command => command.PhotoAttachments is not null);

        RuleFor(command => command.TrialScheduleStatus)
            .NotEmpty()
            .When(command => command.TrialScheduledAtUtc is not null);

        RuleForEach(command => command.OverrideMeasurements!)
            .Must(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value > 0)
            .WithMessage("Override measurements must have keys and positive values.")
            .When(command => command.OverrideMeasurements is not null);
    }
}
