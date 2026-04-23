using Microsoft.AspNetCore.Mvc;
using STailor.Core.Application.Abstractions.Services;
using STailor.Core.Application.Migration;
using STailor.Shared.Contracts.Migration;

namespace STailor.Api.Controllers;

[ApiController]
[Route("api/migration")]
public sealed class MigrationController : ControllerBase
{
    private readonly ILegacyMigrationService _legacyMigrationService;

    public MigrationController(ILegacyMigrationService legacyMigrationService)
    {
        _legacyMigrationService = legacyMigrationService;
    }

    [HttpPost("import")]
    public async Task<ActionResult<LegacyMigrationReportDto>> Import(
        [FromBody] LegacyMigrationImportRequest request,
        CancellationToken cancellationToken)
    {
        var batch = new LegacyMigrationBatch(
            request.Customers?.Select(ToDomainCustomer).ToList() ?? [],
            request.Orders?.Select(ToDomainOrder).ToList() ?? [],
            request.ImportInactiveCustomers,
            request.ImportClosedOrders);

        var report = await _legacyMigrationService.ImportAsync(batch, cancellationToken);
        return Ok(ToDto(report));
    }

    private static LegacyCustomerRecord ToDomainCustomer(LegacyCustomerMigrationDto customer)
    {
        return new LegacyCustomerRecord(
            customer.LegacyId,
            customer.FullName,
            customer.Phone,
            customer.City,
            customer.Comment,
            customer.IsActive);
    }

    private static LegacyOrderRecord ToDomainOrder(LegacyOrderMigrationDto order)
    {
        return new LegacyOrderRecord(
            order.LegacyId,
            order.LegacyCustomerId,
            order.Description,
            order.RecievedOn,
            order.AmountCharged,
            order.AmountPaid,
            order.CollectingOn,
            order.IsOpen);
    }

    private static LegacyMigrationReportDto ToDto(LegacyMigrationReport report)
    {
        return new LegacyMigrationReportDto(
            report.InputCustomerCount,
            report.InputOrderCount,
            report.FilteredCustomerCount,
            report.FilteredOrderCount,
            report.ImportedCustomerCount,
            report.ImportedOrderCount,
            report.SkippedInactiveCustomerCount,
            report.SkippedClosedOrderCount,
            report.SourceChargedTotal,
            report.SourcePaidTotal,
            report.ImportedChargedTotal,
            report.ImportedPaidTotal,
            report.ImportedBalanceTotal,
            report.Issues
                .Select(issue => new LegacyMigrationIssueDto(issue.EntityType, issue.LegacyId, issue.Message))
                .ToList());
    }
}
