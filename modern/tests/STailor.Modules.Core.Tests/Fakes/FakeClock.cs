using STailor.Core.Common.Time;

namespace STailor.Modules.Core.Tests.Fakes;

internal sealed class FakeClock : IClock
{
    public FakeClock(DateTimeOffset utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTimeOffset UtcNow { get; set; }
}
