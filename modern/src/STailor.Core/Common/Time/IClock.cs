namespace STailor.Core.Common.Time;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
