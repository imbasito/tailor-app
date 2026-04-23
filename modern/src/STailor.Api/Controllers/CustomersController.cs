using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using STailor.Core.Application.Abstractions.Services;
using STailor.Core.Application.Commands;
using STailor.Core.Application.ReadModels;
using STailor.Core.Domain.Entities;
using STailor.Core.Domain.Exceptions;
using STailor.Shared.Contracts.Customers;
using STailor.Shared.Contracts.Measurements;

namespace STailor.Api.Controllers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;

    public CustomersController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CustomerWorkspaceItemDto>>> GetCustomers(
        [FromQuery] string? search = null,
        [FromQuery] int maxItems = 100,
        CancellationToken cancellationToken = default)
    {
        if (maxItems <= 0 || maxItems > 500)
        {
            return BadRequest(new { error = "Max items must be between 1 and 500." });
        }

        try
        {
            var customers = await _customerService.GetWorklistAsync(search, maxItems, cancellationToken);
            return Ok(customers.Select(ToWorkspaceItemDto).ToArray());
        }
        catch (DomainRuleViolationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpGet("{customerId:guid}")]
    public async Task<ActionResult<CustomerWorkspaceDetailDto>> GetCustomer(
        Guid customerId,
        [FromQuery] int recentOrderLimit = 5,
        CancellationToken cancellationToken = default)
    {
        if (recentOrderLimit <= 0 || recentOrderLimit > 50)
        {
            return BadRequest(new { error = "Recent order limit must be between 1 and 50." });
        }

        try
        {
            var customer = await _customerService.GetWorkspaceDetailAsync(
                customerId,
                recentOrderLimit,
                cancellationToken);

            if (customer is null)
            {
                return NotFound(new { error = "Customer profile was not found." });
            }

            return Ok(ToWorkspaceDetailDto(customer));
        }
        catch (DomainRuleViolationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<CustomerProfileDto>> CreateCustomer(
        [FromBody] CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var customer = await _customerService.CreateAsync(
                new CreateCustomerCommand(
                    request.FullName,
                    request.PhoneNumber,
                    request.City,
                    request.Notes),
                cancellationToken);

            return CreatedAtAction(nameof(CreateCustomer), ToDto(customer));
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

    [HttpPut("{customerId:guid}")]
    public async Task<ActionResult<CustomerProfileDto>> UpdateCustomer(
        Guid customerId,
        [FromBody] UpdateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var customer = await _customerService.UpdateAsync(
                new UpdateCustomerCommand(
                    customerId,
                    request.FullName,
                    request.PhoneNumber,
                    request.City,
                    request.Notes),
                cancellationToken);

            return Ok(ToDto(customer));
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

    [HttpPut("{customerId:guid}/measurements")]
    public async Task<ActionResult<CustomerProfileDto>> UpsertMeasurements(
        Guid customerId,
        [FromBody] MeasurementSetDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var customer = await _customerService.UpsertBaselineMeasurementsAsync(
                new UpsertBaselineMeasurementsCommand(
                    customerId,
                    request.GarmentType,
                    request.Measurements),
                cancellationToken);

            return Ok(ToDto(customer));
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

    [HttpDelete("{customerId:guid}")]
    public async Task<IActionResult> DeleteCustomer(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _customerService.DeleteAsync(customerId, cancellationToken);
            return NoContent();
        }
        catch (DomainRuleViolationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    private static CustomerProfileDto ToDto(CustomerProfile customerProfile)
    {
        return new CustomerProfileDto(
            customerProfile.Id,
            customerProfile.FullName,
            customerProfile.PhoneNumber,
            customerProfile.City,
            customerProfile.Notes,
            customerProfile.BaselineMeasurementsJson,
            customerProfile.CreatedAtUtc,
            customerProfile.UpdatedAtUtc);
    }

    private static CustomerWorkspaceItemDto ToWorkspaceItemDto(CustomerWorkspaceItem customer)
    {
        return new CustomerWorkspaceItemDto(
            customer.CustomerId,
            customer.FullName,
            customer.PhoneNumber,
            customer.City,
            customer.Notes,
            customer.OrderCount,
            customer.OutstandingBalance,
            customer.LastOrderReceivedAtUtc,
            customer.UpdatedAtUtc);
    }

    private static CustomerWorkspaceDetailDto ToWorkspaceDetailDto(CustomerWorkspaceDetail customer)
    {
        return new CustomerWorkspaceDetailDto(
            customer.CustomerId,
            customer.FullName,
            customer.PhoneNumber,
            customer.City,
            customer.Notes,
            customer.BaselineMeasurementsJson,
            customer.OutstandingBalance,
            customer.CreatedAtUtc,
            customer.UpdatedAtUtc,
            customer.RecentOrders
                .Select(order => new CustomerWorkspaceOrderDto(
                    order.OrderId,
                    order.GarmentType,
                    order.Status,
                    order.AmountCharged,
                    order.AmountPaid,
                    order.BalanceDue,
                    order.ReceivedAtUtc,
                    order.DueAtUtc,
                    order.MeasurementSnapshotJson))
                .ToArray());
    }
}
