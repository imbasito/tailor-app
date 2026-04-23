using FluentValidation;
using STailor.Core.Application.Commands;

namespace STailor.Modules.Core.Validation;

public sealed class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(command => command.FullName)
            .NotEmpty()
            .MaximumLength(120);

        RuleFor(command => command.PhoneNumber)
            .NotEmpty()
            .MaximumLength(30);

        RuleFor(command => command.City)
            .NotEmpty()
            .MaximumLength(120);

        RuleFor(command => command.Notes)
            .MaximumLength(500)
            .When(command => !string.IsNullOrWhiteSpace(command.Notes));
    }
}
