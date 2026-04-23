using STailor.Core.Application.Abstractions.Services;

namespace STailor.Modules.Core.Services;

public sealed class UpdatedAtSyncConflictResolver : ISyncConflictResolver
{
    public bool ShouldApplyRemote(DateTimeOffset localUpdatedAtUtc, DateTimeOffset remoteUpdatedAtUtc)
    {
        return remoteUpdatedAtUtc > localUpdatedAtUtc;
    }
}
