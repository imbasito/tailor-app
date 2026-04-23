using STailor.Core.Application.Abstractions.Services;
using STailor.Core.Application.Migration;

namespace STailor.Modules.Core.Services;

public sealed class LegacyMigrationService : ILegacyMigrationService
{
    private readonly ILegacyMigrationMapper _mapper;
    private readonly ICustomerService _customerService;
    private readonly IOrderService _orderService;

    public LegacyMigrationService(
        ILegacyMigrationMapper mapper,
        ICustomerService customerService,
        IOrderService orderService)
    {
        _mapper = mapper;
        _customerService = customerService;
        _orderService = orderService;
    }

    public async Task<LegacyMigrationReport> ImportAsync(
        LegacyMigrationBatch batch,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var issues = new List<LegacyMigrationIssue>();
        var customerMap = new Dictionary<int, Guid>();

        var inputCustomers = batch.Customers ?? Array.Empty<LegacyCustomerRecord>();
        var inputOrders = batch.Orders ?? Array.Empty<LegacyOrderRecord>();

        var customersToImport = inputCustomers
            .Where(record => batch.ImportInactiveCustomers || record.IsActive)
            .ToList();
        var ordersToImport = inputOrders
            .Where(record => batch.ImportClosedOrders || record.IsOpen)
            .ToList();

        var skippedInactiveCustomerCount = inputCustomers.Count - customersToImport.Count;
        var skippedClosedOrderCount = inputOrders.Count - ordersToImport.Count;

        var importedCustomerCount = 0;
        foreach (var customerRecord in customersToImport)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var command = _mapper.MapCustomer(customerRecord);
                var customer = await _customerService.CreateAsync(command, cancellationToken);
                customerMap[customerRecord.LegacyId] = customer.Id;
                importedCustomerCount++;
            }
            catch (Exception exception)
            {
                issues.Add(new LegacyMigrationIssue(
                    EntityType: nameof(LegacyCustomerRecord),
                    LegacyId: customerRecord.LegacyId,
                    Message: exception.Message));
            }
        }

        var importedOrderCount = 0;
        decimal sourceChargedTotal = 0m;
        decimal sourcePaidTotal = 0m;
        decimal importedChargedTotal = 0m;
        decimal importedPaidTotal = 0m;

        foreach (var orderRecord in ordersToImport)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!customerMap.TryGetValue(orderRecord.LegacyCustomerId, out var mappedCustomerId))
            {
                issues.Add(new LegacyMigrationIssue(
                    EntityType: nameof(LegacyOrderRecord),
                    LegacyId: orderRecord.LegacyId,
                    Message: $"Referenced customer id {orderRecord.LegacyCustomerId} was not migrated."));
                continue;
            }

            try
            {
                var command = _mapper.MapOrder(orderRecord, mappedCustomerId);
                sourceChargedTotal += command.AmountCharged;
                sourcePaidTotal += command.InitialDeposit;

                var order = await _orderService.CreateOrderAsync(command, cancellationToken);
                importedOrderCount++;
                importedChargedTotal += order.AmountCharged;
                importedPaidTotal += order.AmountPaid;
            }
            catch (Exception exception)
            {
                issues.Add(new LegacyMigrationIssue(
                    EntityType: nameof(LegacyOrderRecord),
                    LegacyId: orderRecord.LegacyId,
                    Message: exception.Message));
            }
        }

        if (sourceChargedTotal != importedChargedTotal)
        {
            issues.Add(new LegacyMigrationIssue(
                EntityType: "Parity",
                LegacyId: 0,
                Message: $"Charged totals mismatch. Source: {sourceChargedTotal}, imported: {importedChargedTotal}."));
        }

        if (sourcePaidTotal != importedPaidTotal)
        {
            issues.Add(new LegacyMigrationIssue(
                EntityType: "Parity",
                LegacyId: 0,
                Message: $"Paid totals mismatch. Source: {sourcePaidTotal}, imported: {importedPaidTotal}."));
        }

        return new LegacyMigrationReport(
            InputCustomerCount: inputCustomers.Count,
            InputOrderCount: inputOrders.Count,
            FilteredCustomerCount: customersToImport.Count,
            FilteredOrderCount: ordersToImport.Count,
            ImportedCustomerCount: importedCustomerCount,
            ImportedOrderCount: importedOrderCount,
            SkippedInactiveCustomerCount: skippedInactiveCustomerCount,
            SkippedClosedOrderCount: skippedClosedOrderCount,
            SourceChargedTotal: sourceChargedTotal,
            SourcePaidTotal: sourcePaidTotal,
            ImportedChargedTotal: importedChargedTotal,
            ImportedPaidTotal: importedPaidTotal,
            ImportedBalanceTotal: importedChargedTotal - importedPaidTotal,
            Issues: issues.AsReadOnly());
    }
}
