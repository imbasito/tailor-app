using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using STailor.Core.Application.ReadModels;
using STailor.Core.Application.Abstractions.Services;
using STailor.Core.Application.Commands;
using STailor.Core.Domain.Entities;
using STailor.Core.Domain.Enums;
using STailor.Core.Domain.Exceptions;
using STailor.Shared.Contracts.Orders;

namespace STailor.Api.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpPost]
    public async Task<ActionResult<OrderDto>> CreateOrder(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var order = await _orderService.CreateOrderAsync(
                new CreateOrderCommand(
                    CustomerId: request.CustomerId,
                    GarmentType: request.GarmentType,
                    OverrideMeasurements: request.OverrideMeasurements,
                    AmountCharged: request.AmountCharged,
                    InitialDeposit: request.InitialDeposit,
                    DueAtUtc: request.DueAtUtc,
                    PhotoAttachments: request.PhotoAttachments?.Select(attachment =>
                        new CreateOrderPhotoAttachmentCommand(
                            attachment.FileName,
                            attachment.ResourcePath,
                            attachment.Notes))
                        .ToArray(),
                    TrialScheduledAtUtc: request.TrialScheduledAtUtc,
                    TrialScheduleStatus: request.TrialScheduleStatus,
                    ApplyTrialStatusTransition: request.ApplyTrialStatusTransition),
                cancellationToken);

            return Ok(ToDto(order));
        }
        catch (ValidationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (DomainRuleViolationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpPost("{orderId:guid}/payments")]
    public async Task<ActionResult<OrderDto>> AddPayment(
        Guid orderId,
        [FromBody] AddPaymentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var order = await _orderService.AddPaymentAsync(
                new AddPaymentCommand(orderId, request.Amount, request.PaidAtUtc, request.Note),
                cancellationToken);

            return Ok(ToDto(order));
        }
        catch (ValidationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (DomainRuleViolationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpPost("{orderId:guid}/status")]
    public async Task<ActionResult<OrderDto>> TransitionStatus(
        Guid orderId,
        [FromBody] TransitionOrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseOrderStatus(request.TargetStatus, out var targetStatus))
        {
            return BadRequest(new { error = "Unknown order status." });
        }

        try
        {
            var order = await _orderService.TransitionStatusAsync(
                new TransitionOrderStatusCommand(orderId, targetStatus),
                cancellationToken);

            return Ok(ToDto(order));
        }
        catch (ValidationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (DomainRuleViolationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpPost("{orderId:guid}/schedule-trial")]
    public async Task<ActionResult<OrderDto>> ScheduleTrial(
        Guid orderId,
        [FromBody] ScheduleTrialFittingRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var order = await _orderService.ScheduleTrialFittingAsync(
                new ScheduleTrialFittingCommand(
                    orderId,
                    request.TrialAtUtc,
                    request.ScheduleStatus,
                    request.ApplyTrialStatusTransition),
                cancellationToken);

            return Ok(ToDto(order));
        }
        catch (ValidationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (DomainRuleViolationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpGet("{orderId:guid}")]
    public async Task<ActionResult<OrderWorkspaceDetailDto>> GetOrder(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        try
        {
            var order = await _orderService.GetWorkspaceDetailAsync(orderId, cancellationToken);
            if (order is null)
            {
                return NotFound(new { error = "Order was not found." });
            }

            return Ok(ToWorkspaceDetailDto(order));
        }
        catch (DomainRuleViolationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpDelete("{orderId:guid}")]
    public async Task<IActionResult> DeleteOrder(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _orderService.DeleteAsync(orderId, cancellationToken);
            return NoContent();
        }
        catch (DomainRuleViolationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpGet("reminders")]
    public async Task<ActionResult<IReadOnlyList<OrderReminderDto>>> GetReminders(
        [FromQuery] DateTimeOffset? dueOnOrBeforeUtc,
        [FromQuery] int maxItems = 50,
        CancellationToken cancellationToken = default)
    {
        if (maxItems <= 0 || maxItems > 500)
        {
            return BadRequest(new { error = "Max items must be between 1 and 500." });
        }

        var dueCutoffUtc = dueOnOrBeforeUtc ?? DateTimeOffset.UtcNow.Date.AddDays(8).AddTicks(-1);

        try
        {
            var candidates = await _orderService.GetReminderCandidatesAsync(
                dueCutoffUtc,
                maxItems,
                cancellationToken);

            return Ok(candidates.Select(ToReminderDto).ToArray());
        }
        catch (DomainRuleViolationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpGet("worklist")]
    public async Task<ActionResult<IReadOnlyList<OrderWorklistItemDto>>> GetWorklist(
        [FromQuery] bool includeDelivered = false,
        [FromQuery] string? status = null,
        [FromQuery] bool overdueOnly = false,
        [FromQuery] DateTimeOffset? dueOnOrBeforeUtc = null,
        [FromQuery] string? search = null,
        [FromQuery] int maxItems = 100,
        CancellationToken cancellationToken = default)
    {
        if (maxItems <= 0 || maxItems > 500)
        {
            return BadRequest(new { error = "Max items must be between 1 and 500." });
        }

        OrderStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (IsAnyStatusAlias(status))
            {
                statusFilter = null;
            }
            else
            {
                if (!TryParseOrderStatus(status, out var parsedStatus))
                {
                    return BadRequest(new { error = "Unknown order status." });
                }

                statusFilter = parsedStatus;
            }
        }

        try
        {
            var items = await _orderService.GetWorklistAsync(
                includeDelivered,
                maxItems,
                statusFilter,
                overdueOnly,
                dueOnOrBeforeUtc,
                search,
                cancellationToken);

            return Ok(items.Select(ToWorklistDto).ToArray());
        }
        catch (DomainRuleViolationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    private static OrderDto ToDto(Order order)
    {
        return new OrderDto(
            order.Id,
            order.CustomerProfileId,
            order.GarmentType,
            order.Status.ToString(),
            order.AmountCharged,
            order.AmountPaid,
            order.BalanceDue,
            order.ReceivedAtUtc,
            order.DueAtUtc,
            order.MeasurementSnapshotJson,
            order.TrialScheduledAtUtc,
            order.TrialScheduleStatus,
            order.PhotoAttachmentsJson);
    }

    private static OrderReminderDto ToReminderDto(OrderReminderCandidate candidate)
    {
        return new OrderReminderDto(
            candidate.OrderId,
            candidate.CustomerId,
            candidate.CustomerName,
            candidate.PhoneNumber,
            candidate.GarmentType,
            candidate.Status,
            candidate.AmountCharged,
            candidate.AmountPaid,
            candidate.BalanceDue,
            candidate.DueAtUtc);
    }

    private static OrderWorklistItemDto ToWorklistDto(OrderWorklistItem item)
    {
        return new OrderWorklistItemDto(
            item.OrderId,
            item.CustomerId,
            item.CustomerName,
            item.PhoneNumber,
            item.City,
            item.GarmentType,
            item.Status,
            item.AmountCharged,
            item.AmountPaid,
            item.BalanceDue,
            item.ReceivedAtUtc,
            item.DueAtUtc,
            item.TrialScheduledAtUtc,
            item.TrialScheduleStatus);
    }

    private static OrderWorkspaceDetailDto ToWorkspaceDetailDto(OrderWorkspaceDetail order)
    {
        return new OrderWorkspaceDetailDto(
            order.OrderId,
            order.CustomerId,
            order.CustomerName,
            order.PhoneNumber,
            order.City,
            order.GarmentType,
            order.Status,
            order.AmountCharged,
            order.AmountPaid,
            order.BalanceDue,
            order.ReceivedAtUtc,
            order.DueAtUtc,
            order.MeasurementSnapshotJson,
            order.PhotoAttachmentsJson,
            order.TrialScheduledAtUtc,
            order.TrialScheduleStatus,
            order.Payments
                .Select(payment => new OrderPaymentHistoryItemDto(
                    payment.PaymentId,
                    payment.Amount,
                    payment.PaidAtUtc,
                    payment.Note))
                .ToArray());
    }

    private static bool TryParseOrderStatus(string? value, out OrderStatus status)
    {
        status = default;

        var canonicalStatus = NormalizeStatusToken(value) switch
        {
            "new" => nameof(OrderStatus.New),
            "inprogress" or "progress" => nameof(OrderStatus.InProgress),
            "trialfitting" or "trialfit" or "fitting" => nameof(OrderStatus.TrialFitting),
            "rework" => nameof(OrderStatus.Rework),
            "ready" => nameof(OrderStatus.Ready),
            "delivered" => nameof(OrderStatus.Delivered),
            _ => null,
        };

        return canonicalStatus is not null
            && Enum.TryParse<OrderStatus>(canonicalStatus, out status)
            && Enum.IsDefined(status);
    }

    private static bool IsAnyStatusAlias(string? value)
    {
        var normalized = NormalizeStatusToken(value);
        return normalized is "any" or "all";
    }

    private static string NormalizeStatusToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var buffer = new char[value.Length];
        var index = 0;
        foreach (var character in value)
        {
            if (!char.IsLetterOrDigit(character))
            {
                continue;
            }

            buffer[index++] = char.ToLowerInvariant(character);
        }

        return index == 0
            ? string.Empty
            : new string(buffer, 0, index);
    }
}
