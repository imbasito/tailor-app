namespace STailor.Core.Application.Abstractions.Services;

public interface ISyncConflictResolver
{
    bool ShouldApplyRemote(DateTimeOffset localUpdatedAtUtc, DateTimeOffset remoteUpdatedAtUtc);
}
