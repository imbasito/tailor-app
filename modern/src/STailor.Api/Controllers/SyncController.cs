using Microsoft.AspNetCore.Mvc;
using STailor.Core.Application.Abstractions.Services;
using STailor.Shared.Contracts.Sync;

namespace STailor.Api.Controllers;

[ApiController]
[Route("api/sync")]
public sealed class SyncController : ControllerBase
{
    private readonly ISyncQueueService _syncQueueService;

    public SyncController(ISyncQueueService syncQueueService)
    {
        _syncQueueService = syncQueueService;
    }

    [HttpGet("diagnostics")]
    public async Task<ActionResult<SyncQueueDiagnosticsDto>> GetDiagnostics(
        CancellationToken cancellationToken = default)
    {
        var diagnostics = await _syncQueueService.GetDiagnosticsAsync(cancellationToken);

        return Ok(new SyncQueueDiagnosticsDto(
            diagnostics.PendingCount,
            diagnostics.FailedCount,
            diagnostics.SyncedCount,
            diagnostics.RetryDueCount,
            diagnostics.TotalCount,
            diagnostics.OldestPendingEnqueuedAtUtc,
            diagnostics.EvaluatedAtUtc));
    }
}
