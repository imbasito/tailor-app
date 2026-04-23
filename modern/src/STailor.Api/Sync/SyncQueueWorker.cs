using Microsoft.Extensions.Options;
using STailor.Core.Common.Time;
using STailor.Core.Application.Abstractions.Services;

namespace STailor.Api.Sync;

public sealed class SyncQueueWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<SyncWorkerOptions> _options;
    private readonly IClock _clock;
    private readonly ILogger<SyncQueueWorker> _logger;

    public SyncQueueWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<SyncWorkerOptions> options,
        IClock clock,
        ILogger<SyncQueueWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workerOptions = _options.Value;
        if (!workerOptions.Enabled)
        {
            _logger.LogInformation("Sync queue worker is disabled.");
            return;
        }

        var pollInterval = TimeSpan.FromSeconds(Math.Clamp(workerOptions.PollIntervalSeconds, 5, 300));
        var maxBatchSize = Math.Clamp(workerOptions.MaxBatchSize, 1, 200);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var syncQueueService = scope.ServiceProvider.GetRequiredService<ISyncQueueService>();
                var syncQueueDispatcher = scope.ServiceProvider.GetRequiredService<ISyncQueueDispatcher>();
                var centralSyncPullService = scope.ServiceProvider.GetRequiredService<ICentralSyncPullService>();

                var pendingItems = await syncQueueService.GetPendingBatchAsync(maxBatchSize, stoppingToken);
                if (pendingItems.Count > 0)
                {
                    if (workerOptions.DryRun)
                    {
                        _logger.LogInformation(
                            "Sync queue worker dry-run found {PendingCount} due item(s).",
                            pendingItems.Count);
                    }
                    else
                    {
                        var syncedCount = 0;
                        var failedCount = 0;

                        foreach (var item in pendingItems)
                        {
                            try
                            {
                                await syncQueueDispatcher.DispatchAsync(item, stoppingToken);
                                await syncQueueService.MarkSyncedAsync(item.Id, _clock.UtcNow, stoppingToken);
                                syncedCount++;
                            }
                            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                            {
                                throw;
                            }
                            catch (Exception exception)
                            {
                                failedCount++;

                                _logger.LogWarning(
                                    exception,
                                    "Sync queue dispatch failed for item {QueueItemId} ({EntityType}:{Operation}).",
                                    item.Id,
                                    item.EntityType,
                                    item.Operation);

                                try
                                {
                                    await syncQueueService.MarkFailedAsync(
                                        item.Id,
                                        BuildFailureMessage(exception),
                                        stoppingToken);
                                }
                                catch (Exception markFailureException)
                                {
                                    _logger.LogError(
                                        markFailureException,
                                        "Failed to persist sync queue failure state for item {QueueItemId}.",
                                        item.Id);
                                }
                            }
                        }

                        _logger.LogInformation(
                            "Sync queue worker processed {PendingCount} due item(s): synced={SyncedCount}, failed={FailedCount}.",
                            pendingItems.Count,
                            syncedCount,
                            failedCount);
                    }
                }

                if (!workerOptions.DryRun)
                {
                    var pullResult = await centralSyncPullService.PullAsync(maxBatchSize, stoppingToken);
                    if (pullResult.TotalProcessed > 0)
                    {
                        _logger.LogInformation(
                            "Central pull sync processed {TotalProcessed} item(s): applied={TotalApplied}, customers={CustomersApplied}/{CustomersProcessed}, orders={OrdersApplied}/{OrdersProcessed}, payments={PaymentsApplied}/{PaymentsProcessed}.",
                            pullResult.TotalProcessed,
                            pullResult.TotalApplied,
                            pullResult.CustomersApplied,
                            pullResult.CustomersProcessed,
                            pullResult.OrdersApplied,
                            pullResult.OrdersProcessed,
                            pullResult.PaymentsApplied,
                            pullResult.PaymentsProcessed);
                    }
                }

                var diagnostics = await syncQueueService.GetDiagnosticsAsync(stoppingToken);
                _logger.LogDebug(
                    "Sync diagnostics: pending={PendingCount}, failed={FailedCount}, retryDue={RetryDueCount}, synced={SyncedCount}, total={TotalCount}",
                    diagnostics.PendingCount,
                    diagnostics.FailedCount,
                    diagnostics.RetryDueCount,
                    diagnostics.SyncedCount,
                    diagnostics.TotalCount);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Sync queue worker cycle failed.");
            }

            await Task.Delay(pollInterval, stoppingToken);
        }
    }

    private static string BuildFailureMessage(Exception exception)
    {
        const int maxLength = 480;

        var baseMessage = string.IsNullOrWhiteSpace(exception.Message)
            ? exception.GetType().Name
            : $"{exception.GetType().Name}: {exception.Message.Trim()}";

        return baseMessage.Length <= maxLength
            ? baseMessage
            : baseMessage[..maxLength];
    }
}
