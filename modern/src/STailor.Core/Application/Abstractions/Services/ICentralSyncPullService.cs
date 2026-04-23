using STailor.Core.Application.ReadModels;

namespace STailor.Core.Application.Abstractions.Services;

public interface ICentralSyncPullService
{
    Task<CentralSyncPullResult> PullAsync(int maxItems, CancellationToken cancellationToken = default);
}
