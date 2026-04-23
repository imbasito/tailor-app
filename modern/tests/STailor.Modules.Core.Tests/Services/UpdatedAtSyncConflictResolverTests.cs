using STailor.Modules.Core.Services;

namespace STailor.Modules.Core.Tests.Services;

public sealed class UpdatedAtSyncConflictResolverTests
{
    [Fact]
    public void ShouldApplyRemote_WhenRemoteIsNewer_ReturnsTrue()
    {
        var resolver = new UpdatedAtSyncConflictResolver();
        var localUpdatedAt = new DateTimeOffset(2026, 4, 19, 8, 0, 0, TimeSpan.Zero);
        var remoteUpdatedAt = localUpdatedAt.AddSeconds(1);

        var shouldApply = resolver.ShouldApplyRemote(localUpdatedAt, remoteUpdatedAt);

        Assert.True(shouldApply);
    }

    [Fact]
    public void ShouldApplyRemote_WhenLocalIsSameOrNewer_ReturnsFalse()
    {
        var resolver = new UpdatedAtSyncConflictResolver();
        var localUpdatedAt = new DateTimeOffset(2026, 4, 19, 8, 0, 0, TimeSpan.Zero);

        Assert.False(resolver.ShouldApplyRemote(localUpdatedAt, localUpdatedAt));
        Assert.False(resolver.ShouldApplyRemote(localUpdatedAt, localUpdatedAt.AddSeconds(-1)));
    }
}
