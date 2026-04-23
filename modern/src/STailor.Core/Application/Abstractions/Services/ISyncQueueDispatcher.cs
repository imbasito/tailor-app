using STailor.Core.Domain.Entities;

namespace STailor.Core.Application.Abstractions.Services;

public interface ISyncQueueDispatcher
{
    Task DispatchAsync(SyncQueueItem queueItem, CancellationToken cancellationToken = default);
}