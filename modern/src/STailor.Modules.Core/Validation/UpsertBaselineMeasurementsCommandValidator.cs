using FluentValidation;
using STailor.Core.Application.Commands;

namespace STailor.Modules.Core.Validation;

public sealed class UpsertBaselineMeasurementsCommandValidator : AbstractValidator<UpsertBaselineMeasurementsCommand>
{
    public UpsertBaselineMeasurementsCommandValidator()
    {
        RuleFor(command => command.CustomerId)
            .NotEmpty();

        RuleFor(command => command.GarmentType)
            .NotEmpty()
            .MaximumLength(80);

        RuleFor(command => command.Measurements)
            .NotNull()
            .Must(measurements => measurements.Count > 0)
            .WithMessage("At least one measurement is required.");

        RuleForEach(command => command.Measurements)
            .Must(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value > 0)
            .WithMessage("Measurement keys must be present and values must be greater than zero.");
    }
}
