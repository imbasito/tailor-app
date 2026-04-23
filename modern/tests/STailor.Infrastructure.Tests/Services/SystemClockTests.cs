using STailor.Infrastructure.Services;

namespace STailor.Infrastructure.Tests.Services;

public sealed class SystemClockTests
{
    [Fact]
    public void UtcNow_ReturnsUtcTimestamp()
    {
        var clock = new SystemClock();

        var now = clock.UtcNow;

        Assert.Equal(TimeSpan.Zero, now.Offset);
    }
}
