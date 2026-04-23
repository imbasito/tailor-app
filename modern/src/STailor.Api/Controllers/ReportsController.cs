using Microsoft.AspNetCore.Mvc;
using STailor.Core.Application.Abstractions.Services;
using STailor.Core.Application.ReadModels;

namespace STailor.Api.Controllers;

/// <summary>
/// API controller for operational reports.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly IReportingService _reportingService;

    public ReportsController(IReportingService reportingService)
    {
        _reportingService = reportingService;
    }

    /// <summary>
    /// Gets the complete operations report.
    /// </summary>
    [HttpGet("operations")]
    public async Task<ActionResult<OperationsReport>> GetOperationsReport(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] bool includeDelivered = true,
        CancellationToken cancellationToken = default)
    {
        if (fromDate is not null && toDate is not null && fromDate.Value.Date > toDate.Value.Date)
        {
            return BadRequest(new { message = "From date cannot be later than to date." });
        }

        var report = await _reportingService.GetOperationsReportAsync(
            new OperationsReportFilter
            {
                SearchText = search,
                Status = status,
                IncludeDelivered = includeDelivered,
                ReceivedFromUtc = fromDate is null
                    ? null
                    : new DateTimeOffset(fromDate.Value.Date, TimeSpan.Zero),
                ReceivedToUtc = toDate is null
                    ? null
                    : new DateTimeOffset(toDate.Value.Date.AddDays(1).AddTicks(-1), TimeSpan.Zero),
            },
            cancellationToken);

        return Ok(report);
    }

    /// <summary>
    /// Gets the daily orders report.
    /// </summary>
    [HttpGet("daily-orders")]
    public async Task<ActionResult<DailyOrdersReport>> GetDailyOrdersReport(
        [FromQuery] DateTime? date,
        CancellationToken cancellationToken)
    {
        var report = await _reportingService.GetDailyOrdersReportAsync(date, cancellationToken);
        return Ok(report);
    }

    /// <summary>
    /// Gets the outstanding dues/receivables report.
    /// </summary>
    [HttpGet("outstanding-dues")]
    public async Task<ActionResult<OutstandingDuesReport>> GetOutstandingDuesReport(
        [FromQuery] decimal? minBalanceDue,
        [FromQuery] decimal? maxBalanceDue,
        [FromQuery] string? status,
        [FromQuery] bool overdueOnly = false,
        [FromQuery] string orderBy = "BalanceDesc",
        CancellationToken cancellationToken = default)
    {
        var filter = new OutstandingDuesFilter
        {
            MinBalanceDue = minBalanceDue,
            MaxBalanceDue = maxBalanceDue,
            Status = status,
            OverdueOnly = overdueOnly,
            OrderBy = orderBy
        };

        var report = await _reportingService.GetOutstandingDuesReportAsync(filter, cancellationToken);
        return Ok(report);
    }

    /// <summary>
    /// Gets customer measurement history.
    /// </summary>
    [HttpGet("customers/{customerId:guid}/measurement-history")]
    public async Task<ActionResult<CustomerMeasurementHistoryReport>> GetCustomerMeasurementHistory(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        try
        {
            var report = await _reportingService.GetCustomerMeasurementHistoryAsync(customerId, cancellationToken);
            return Ok(report);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Gets the delivery queue for a date range.
    /// </summary>
    [HttpGet("delivery-queue")]
    public async Task<ActionResult<DeliveryQueueReport>> GetDeliveryQueue(
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate,
        CancellationToken cancellationToken)
    {
        if (fromDate > toDate)
        {
            return BadRequest(new { message = "From date cannot be later than to date." });
        }

        if ((toDate - fromDate).TotalDays > 31)
        {
            return BadRequest(new { message = "Date range cannot exceed 31 days." });
        }

        var report = await _reportingService.GetDeliveryQueueAsync(fromDate, toDate, cancellationToken);
        return Ok(report);
    }
}
