namespace STailor.Core.Domain.Exceptions;

public sealed class DomainRuleViolationException : Exception
{
    public DomainRuleViolationException(string message)
        : base(message)
    {
    }
}
