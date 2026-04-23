using Microsoft.EntityFrameworkCore;
using STailor.Core.Application.Abstractions.Services;
using STailor.Core.Common.Entities;
using STailor.Core.Domain.Entities;
using STailor.Core.Domain.Exceptions;
using STailor.Infrastructure.Persistence;

namespace STailor.Infrastructure.Services;

public sealed class CentralSyncQueueDispatcher : ISyncQueueDispatcher
{
    private readonly LocalTailorDbContext _localDbContext;
    private readonly CentralTailorDbContext _centralDbContext;
    private readonly ISyncConflictResolver _syncConflictResolver;

    public CentralSyncQueueDispatcher(
        LocalTailorDbContext localDbContext,
        CentralTailorDbContext centralDbContext,
        ISyncConflictResolver syncConflictResolver)
    {
        _localDbContext = localDbContext;
        _centralDbContext = centralDbContext;
        _syncConflictResolver = syncConflictResolver;
    }

    public async Task DispatchAsync(SyncQueueItem queueItem, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queueItem);

        var normalizedOperation = NormalizeOperation(queueItem.Operation);
        var normalizedEntityType = NormalizeEntityType(queueItem.EntityType);

        switch (normalizedOperation)
        {
            case "upsert":
                await UpsertAsync(normalizedEntityType, queueItem.EntityId, cancellationToken);
                break;
            case "delete":
                await DeleteAsync(
                    normalizedEntityType,
                    queueItem.EntityId,
                    queueItem.EntityUpdatedAtUtc,
                    cancellationToken);
                break;
            default:
                throw new DomainRuleViolationException($"Unsupported sync operation '{queueItem.Operation}'.");
        }

        await _centralDbContext.SaveChangesAsync(cancellationToken);
    }

    private Task UpsertAsync(string normalizedEntityType, Guid entityId, CancellationToken cancellationToken)
    {
        return normalizedEntityType switch
        {
            "customerprofile" or "customer" => UpsertCustomerProfileAsync(entityId, cancellationToken),
            "order" => UpsertOrderAsync(entityId, includePayments: true, cancellationToken),
            "payment" => UpsertPaymentAsync(entityId, cancellationToken),
            _ => throw new DomainRuleViolationException($"Unsupported sync entity type '{normalizedEntityType}'.")
        };
    }

    private Task DeleteAsync(
        string normalizedEntityType,
        Guid entityId,
        DateTimeOffset deletedAtUtc,
        CancellationToken cancellationToken)
    {
        return normalizedEntityType switch
        {
            "customerprofile" or "customer" => DeleteCustomerProfileAsync(entityId, deletedAtUtc, cancellationToken),
            "order" => DeleteOrderAsync(entityId, deletedAtUtc, cancellationToken),
            "payment" => DeletePaymentAsync(entityId, deletedAtUtc, cancellationToken),
            _ => throw new DomainRuleViolationException($"Unsupported sync entity type '{normalizedEntityType}'.")
        };
    }

    private async Task UpsertCustomerProfileAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var localCustomer = await _localDbContext.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(customer => customer.Id == customerId, cancellationToken)
            ?? throw new DomainRuleViolationException(
                $"Customer profile {customerId} was not found in local store.");

        var centralCustomer = await _centralDbContext.CustomerProfiles
            .FirstOrDefaultAsync(customer => customer.Id == customerId, cancellationToken);

        if (centralCustomer is null)
        {
            var newCustomer = new CustomerProfile(
                localCustomer.FullName,
                localCustomer.PhoneNumber,
                localCustomer.City,
                localCustomer.Notes);

            newCustomer.SetBaselineMeasurements(localCustomer.BaselineMeasurementsJson);

            var entry = await _centralDbContext.CustomerProfiles.AddAsync(newCustomer, cancellationToken);
            entry.CurrentValues.SetValues(localCustomer);
            await RemoveDeletionTombstoneAsync("customerprofile", customerId, cancellationToken);
            return;
        }

        if (!ShouldApplyIncomingChange(localCustomer, centralCustomer))
        {
            return;
        }

        _centralDbContext.Entry(centralCustomer).CurrentValues.SetValues(localCustomer);
        await RemoveDeletionTombstoneAsync("customerprofile", customerId, cancellationToken);
    }

    private async Task UpsertOrderAsync(Guid orderId, bool includePayments, CancellationToken cancellationToken)
    {
        var localOrder = await _localDbContext.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(order => order.Id == orderId, cancellationToken)
            ?? throw new DomainRuleViolationException($"Order {orderId} was not found in local store.");

        await UpsertCustomerProfileAsync(localOrder.CustomerProfileId, cancellationToken);

        var centralOrder = await _centralDbContext.Orders
            .FirstOrDefaultAsync(order => order.Id == orderId, cancellationToken);

        if (centralOrder is null)
        {
            var newOrder = new Order(
                localOrder.CustomerProfileId,
                localOrder.GarmentType,
                localOrder.MeasurementSnapshotJson,
                localOrder.AmountCharged,
                localOrder.ReceivedAtUtc,
                localOrder.DueAtUtc,
                localOrder.PhotoAttachmentsJson);

            var entry = await _centralDbContext.Orders.AddAsync(newOrder, cancellationToken);
            entry.CurrentValues.SetValues(localOrder);
        }
        else
        {
            if (!ShouldApplyIncomingChange(localOrder, centralOrder))
            {
                return;
            }

            _centralDbContext.Entry(centralOrder).CurrentValues.SetValues(localOrder);
        }

        await RemoveDeletionTombstoneAsync("order", orderId, cancellationToken);

        if (includePayments)
        {
            await SyncOrderPaymentsAsync(localOrder.Id, cancellationToken);
        }
    }

    private async Task UpsertPaymentAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var localPayment = await _localDbContext.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(payment => payment.Id == paymentId, cancellationToken)
            ?? throw new DomainRuleViolationException($"Payment {paymentId} was not found in local store.");

        await UpsertOrderAsync(localPayment.OrderId, includePayments: false, cancellationToken);
        await UpsertPaymentRecordAsync(localPayment, cancellationToken);
    }

    private async Task SyncOrderPaymentsAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var localPayments = await _localDbContext.Payments
            .AsNoTracking()
            .Where(payment => payment.OrderId == orderId)
            .ToListAsync(cancellationToken);

        foreach (var localPayment in localPayments)
        {
            await UpsertPaymentRecordAsync(localPayment, cancellationToken);
        }
    }

    private async Task UpsertPaymentRecordAsync(Payment localPayment, CancellationToken cancellationToken)
    {
        var centralPayment = await _centralDbContext.Payments
            .FirstOrDefaultAsync(payment => payment.Id == localPayment.Id, cancellationToken);

        if (centralPayment is null)
        {
            var newPayment = new Payment(
                localPayment.OrderId,
                localPayment.Amount,
                localPayment.PaidAtUtc,
                localPayment.Note);

            var entry = await _centralDbContext.Payments.AddAsync(newPayment, cancellationToken);
            entry.CurrentValues.SetValues(localPayment);
            await RemoveDeletionTombstoneAsync("payment", localPayment.Id, cancellationToken);
            return;
        }

        if (!ShouldApplyIncomingChange(localPayment, centralPayment))
        {
            return;
        }

        _centralDbContext.Entry(centralPayment).CurrentValues.SetValues(localPayment);
        await RemoveDeletionTombstoneAsync("payment", localPayment.Id, cancellationToken);
    }

    private async Task DeleteCustomerProfileAsync(
        Guid customerId,
        DateTimeOffset deletedAtUtc,
        CancellationToken cancellationToken)
    {
        var centralCustomer = await _centralDbContext.CustomerProfiles
            .FirstOrDefaultAsync(customer => customer.Id == customerId, cancellationToken);

        if (centralCustomer is null || !ShouldApplyDelete(deletedAtUtc, centralCustomer))
        {
            return;
        }

        var customerOrders = await _centralDbContext.Orders
            .Where(order => order.CustomerProfileId == customerId)
            .ToListAsync(cancellationToken);

        if (customerOrders.Count > 0)
        {
            _centralDbContext.Orders.RemoveRange(customerOrders);
        }

        _centralDbContext.CustomerProfiles.Remove(centralCustomer);
        await UpsertDeletionTombstoneAsync("customerprofile", customerId, deletedAtUtc, cancellationToken);
    }

    private async Task DeleteOrderAsync(
        Guid orderId,
        DateTimeOffset deletedAtUtc,
        CancellationToken cancellationToken)
    {
        var centralOrder = await _centralDbContext.Orders
            .FirstOrDefaultAsync(order => order.Id == orderId, cancellationToken);

        if (centralOrder is null || !ShouldApplyDelete(deletedAtUtc, centralOrder))
        {
            return;
        }

        var orderPayments = await _centralDbContext.Payments
            .Where(payment => payment.OrderId == orderId)
            .ToListAsync(cancellationToken);

        if (orderPayments.Count > 0)
        {
            _centralDbContext.Payments.RemoveRange(orderPayments);
        }

        _centralDbContext.Orders.Remove(centralOrder);
        await UpsertDeletionTombstoneAsync("order", orderId, deletedAtUtc, cancellationToken);
    }

    private async Task DeletePaymentAsync(
        Guid paymentId,
        DateTimeOffset deletedAtUtc,
        CancellationToken cancellationToken)
    {
        var centralPayment = await _centralDbContext.Payments
            .FirstOrDefaultAsync(payment => payment.Id == paymentId, cancellationToken);

        if (centralPayment is not null && ShouldApplyDelete(deletedAtUtc, centralPayment))
        {
            _centralDbContext.Payments.Remove(centralPayment);
            await UpsertDeletionTombstoneAsync("payment", paymentId, deletedAtUtc, cancellationToken);
        }
    }

    private async Task UpsertDeletionTombstoneAsync(
        string normalizedEntityType,
        Guid entityId,
        DateTimeOffset deletedAtUtc,
        CancellationToken cancellationToken)
    {
        var existingTombstone = await _centralDbContext.SyncDeletionTombstones
            .FirstOrDefaultAsync(
                item => item.EntityType == normalizedEntityType && item.EntityId == entityId,
                cancellationToken);

        if (existingTombstone is null)
        {
            await _centralDbContext.SyncDeletionTombstones.AddAsync(
                new SyncDeletionTombstone(normalizedEntityType, entityId, deletedAtUtc),
                cancellationToken);
            return;
        }

        if (deletedAtUtc > existingTombstone.DeletedAtUtc)
        {
            _centralDbContext.Entry(existingTombstone)
                .Property(item => item.DeletedAtUtc)
                .CurrentValue = deletedAtUtc;
        }
    }

    private async Task RemoveDeletionTombstoneAsync(
        string normalizedEntityType,
        Guid entityId,
        CancellationToken cancellationToken)
    {
        var existingTombstone = await _centralDbContext.SyncDeletionTombstones
            .FirstOrDefaultAsync(
                item => item.EntityType == normalizedEntityType && item.EntityId == entityId,
                cancellationToken);

        if (existingTombstone is not null)
        {
            _centralDbContext.SyncDeletionTombstones.Remove(existingTombstone);
        }
    }

    private bool ShouldApplyIncomingChange(AuditableEntity incomingEntity, AuditableEntity existingEntity)
    {
        return _syncConflictResolver.ShouldApplyRemote(existingEntity.UpdatedAtUtc, incomingEntity.UpdatedAtUtc);
    }

    private bool ShouldApplyDelete(DateTimeOffset deletedAtUtc, AuditableEntity existingEntity)
    {
        return _syncConflictResolver.ShouldApplyRemote(existingEntity.UpdatedAtUtc, deletedAtUtc);
    }

    private static string NormalizeEntityType(string entityType)
    {
        if (string.IsNullOrWhiteSpace(entityType))
        {
            throw new DomainRuleViolationException("Sync entity type is required.");
        }

        var trimmed = entityType.Trim();
        var dotIndex = trimmed.LastIndexOf('.');
        var tail = dotIndex >= 0 ? trimmed[(dotIndex + 1)..] : trimmed;

        return tail.ToLowerInvariant();
    }

    private static string NormalizeOperation(string operation)
    {
        if (string.IsNullOrWhiteSpace(operation))
        {
            throw new DomainRuleViolationException("Sync operation is required.");
        }

        var normalized = operation.Trim().ToLowerInvariant();
        return normalized == "remove" ? "delete" : normalized;
    }
}
