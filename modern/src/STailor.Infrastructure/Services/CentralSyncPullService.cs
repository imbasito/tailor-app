using Microsoft.EntityFrameworkCore;
using STailor.Core.Application.Abstractions.Services;
using STailor.Core.Application.ReadModels;
using STailor.Core.Common.Entities;
using STailor.Core.Domain.Entities;
using STailor.Core.Domain.Exceptions;
using STailor.Infrastructure.Persistence;

namespace STailor.Infrastructure.Services;

public sealed class CentralSyncPullService : ICentralSyncPullService
{
    private const string CustomerScope = "customer_profiles";
    private const string OrderScope = "orders";
    private const string PaymentScope = "payments";
    private const string TombstoneScope = "deletion_tombstones";

    private readonly LocalTailorDbContext _localDbContext;
    private readonly CentralTailorDbContext _centralDbContext;
    private readonly ISyncConflictResolver _syncConflictResolver;

    public CentralSyncPullService(
        LocalTailorDbContext localDbContext,
        CentralTailorDbContext centralDbContext,
        ISyncConflictResolver syncConflictResolver)
    {
        _localDbContext = localDbContext;
        _centralDbContext = centralDbContext;
        _syncConflictResolver = syncConflictResolver;
    }

    public async Task<CentralSyncPullResult> PullAsync(
        int maxItems,
        CancellationToken cancellationToken = default)
    {
        if (maxItems <= 0)
        {
            throw new DomainRuleViolationException("Max items must be greater than zero.");
        }

        var customerStage = await PullStageAsync(
            CustomerScope,
            _centralDbContext.CustomerProfiles,
            ApplyCustomerProfileAsync,
            maxItems,
            cancellationToken);

        var orderStage = await PullStageAsync(
            OrderScope,
            _centralDbContext.Orders,
            ApplyOrderAsync,
            maxItems,
            cancellationToken);

        var paymentStage = await PullStageAsync(
            PaymentScope,
            _centralDbContext.Payments,
            ApplyPaymentAsync,
            maxItems,
            cancellationToken);

        await PullDeletionTombstonesAsync(maxItems, cancellationToken);

        return new CentralSyncPullResult(
            customerStage.ProcessedCount,
            customerStage.AppliedCount,
            orderStage.ProcessedCount,
            orderStage.AppliedCount,
            paymentStage.ProcessedCount,
            paymentStage.AppliedCount);
    }

    private async Task PullDeletionTombstonesAsync(int maxItems, CancellationToken cancellationToken)
    {
        var existingCursor = await GetExistingCursorAsync(TombstoneScope, cancellationToken);
        var tombstones = await GetDeletionTombstoneBatchAsync(existingCursor?.LastSyncedAtUtc, maxItems, cancellationToken);

        if (tombstones.Count == 0)
        {
            return;
        }

        var cursor = existingCursor;
        if (cursor is null)
        {
            cursor = new SyncPullCursor(TombstoneScope);
            await _localDbContext.SyncPullCursors.AddAsync(cursor, cancellationToken);
        }

        foreach (var tombstone in tombstones)
        {
            await ApplyDeletionTombstoneAsync(tombstone, cancellationToken);
        }

        cursor.Advance(tombstones[^1].DeletedAtUtc);
        await _localDbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<PullStageResult> PullStageAsync<TEntity>(
        string scope,
        DbSet<TEntity> remoteSet,
        Func<TEntity, CancellationToken, Task<bool>> applyAsync,
        int maxItems,
        CancellationToken cancellationToken)
        where TEntity : AuditableEntity
    {
        var existingCursor = await GetExistingCursorAsync(scope, cancellationToken);
        var remoteBatch = await GetRemoteBatchAsync(remoteSet, existingCursor?.LastSyncedAtUtc, maxItems, cancellationToken);

        if (remoteBatch.Count == 0)
        {
            return PullStageResult.Empty;
        }

        var cursor = existingCursor;
        if (cursor is null)
        {
            cursor = new SyncPullCursor(scope);
            await _localDbContext.SyncPullCursors.AddAsync(cursor, cancellationToken);
        }

        var appliedCount = 0;

        foreach (var remoteEntity in remoteBatch)
        {
            if (await applyAsync(remoteEntity, cancellationToken))
            {
                appliedCount++;
            }
        }

        cursor.Advance(remoteBatch[^1].UpdatedAtUtc);
        await _localDbContext.SaveChangesAsync(cancellationToken);

        return new PullStageResult(remoteBatch.Count, appliedCount);
    }

    private async Task<bool> ApplyCustomerProfileAsync(
        CustomerProfile remoteCustomer,
        CancellationToken cancellationToken)
    {
        var localCustomer = await _localDbContext.CustomerProfiles
            .FirstOrDefaultAsync(customer => customer.Id == remoteCustomer.Id, cancellationToken);

        if (localCustomer is null)
        {
            var newCustomer = new CustomerProfile(
                remoteCustomer.FullName,
                remoteCustomer.PhoneNumber,
                remoteCustomer.City,
                remoteCustomer.Notes);

            newCustomer.SetBaselineMeasurements(remoteCustomer.BaselineMeasurementsJson);

            var entry = await _localDbContext.CustomerProfiles.AddAsync(newCustomer, cancellationToken);
            entry.CurrentValues.SetValues(remoteCustomer);
            return true;
        }

        if (!ShouldApplyIncomingChange(localCustomer, remoteCustomer))
        {
            return false;
        }

        _localDbContext.Entry(localCustomer).CurrentValues.SetValues(remoteCustomer);
        return true;
    }

    private async Task<bool> ApplyOrderAsync(Order remoteOrder, CancellationToken cancellationToken)
    {
        await EnsureLocalCustomerProfileAsync(remoteOrder.CustomerProfileId, cancellationToken);

        var localOrder = await _localDbContext.Orders
            .FirstOrDefaultAsync(order => order.Id == remoteOrder.Id, cancellationToken);

        if (localOrder is null)
        {
            var newOrder = new Order(
                remoteOrder.CustomerProfileId,
                remoteOrder.GarmentType,
                remoteOrder.MeasurementSnapshotJson,
                remoteOrder.AmountCharged,
                remoteOrder.ReceivedAtUtc,
                remoteOrder.DueAtUtc,
                remoteOrder.PhotoAttachmentsJson);

            var entry = await _localDbContext.Orders.AddAsync(newOrder, cancellationToken);
            entry.CurrentValues.SetValues(remoteOrder);
            return true;
        }

        if (!ShouldApplyIncomingChange(localOrder, remoteOrder))
        {
            return false;
        }

        _localDbContext.Entry(localOrder).CurrentValues.SetValues(remoteOrder);
        return true;
    }

    private async Task<bool> ApplyPaymentAsync(Payment remotePayment, CancellationToken cancellationToken)
    {
        await EnsureLocalOrderAsync(remotePayment.OrderId, cancellationToken);

        var localPayment = await _localDbContext.Payments
            .FirstOrDefaultAsync(payment => payment.Id == remotePayment.Id, cancellationToken);

        if (localPayment is null)
        {
            var newPayment = new Payment(
                remotePayment.OrderId,
                remotePayment.Amount,
                remotePayment.PaidAtUtc,
                remotePayment.Note);

            var entry = await _localDbContext.Payments.AddAsync(newPayment, cancellationToken);
            entry.CurrentValues.SetValues(remotePayment);
            return true;
        }

        if (!ShouldApplyIncomingChange(localPayment, remotePayment))
        {
            return false;
        }

        _localDbContext.Entry(localPayment).CurrentValues.SetValues(remotePayment);
        return true;
    }

    private async Task EnsureLocalCustomerProfileAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var localCustomerExists = await _localDbContext.CustomerProfiles
            .AnyAsync(customer => customer.Id == customerId, cancellationToken);

        if (localCustomerExists)
        {
            return;
        }

        var remoteCustomer = await _centralDbContext.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(customer => customer.Id == customerId, cancellationToken)
            ?? throw new DomainRuleViolationException(
                $"Customer profile {customerId} was not found in central store.");

        await ApplyCustomerProfileAsync(remoteCustomer, cancellationToken);
        await _localDbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureLocalOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var localOrderExists = await _localDbContext.Orders
            .AnyAsync(order => order.Id == orderId, cancellationToken);

        if (localOrderExists)
        {
            return;
        }

        var remoteOrder = await _centralDbContext.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(order => order.Id == orderId, cancellationToken)
            ?? throw new DomainRuleViolationException($"Order {orderId} was not found in central store.");

        await ApplyOrderAsync(remoteOrder, cancellationToken);
        await _localDbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ApplyDeletionTombstoneAsync(
        SyncDeletionTombstone tombstone,
        CancellationToken cancellationToken)
    {
        switch (NormalizeEntityType(tombstone.EntityType))
        {
            case "customerprofile":
                await DeleteLocalCustomerProfileAsync(tombstone.EntityId, tombstone.DeletedAtUtc, cancellationToken);
                break;
            case "order":
                await DeleteLocalOrderAsync(tombstone.EntityId, tombstone.DeletedAtUtc, cancellationToken);
                break;
            case "payment":
                await DeleteLocalPaymentAsync(tombstone.EntityId, tombstone.DeletedAtUtc, cancellationToken);
                break;
            default:
                break;
        }
    }

    private async Task DeleteLocalCustomerProfileAsync(
        Guid customerId,
        DateTimeOffset deletedAtUtc,
        CancellationToken cancellationToken)
    {
        var localCustomer = await _localDbContext.CustomerProfiles
            .FirstOrDefaultAsync(customer => customer.Id == customerId, cancellationToken);

        if (localCustomer is null || !ShouldApplyDelete(localCustomer, deletedAtUtc))
        {
            return;
        }

        var localOrders = await _localDbContext.Orders
            .Where(order => order.CustomerProfileId == customerId)
            .ToListAsync(cancellationToken);

        if (localOrders.Count > 0)
        {
            var orderIds = localOrders.Select(order => order.Id).ToList();
            var localPayments = await _localDbContext.Payments
                .Where(payment => orderIds.Contains(payment.OrderId))
                .ToListAsync(cancellationToken);

            if (localPayments.Count > 0)
            {
                _localDbContext.Payments.RemoveRange(localPayments);
            }

            _localDbContext.Orders.RemoveRange(localOrders);
        }

        _localDbContext.CustomerProfiles.Remove(localCustomer);
    }

    private async Task DeleteLocalOrderAsync(
        Guid orderId,
        DateTimeOffset deletedAtUtc,
        CancellationToken cancellationToken)
    {
        var localOrder = await _localDbContext.Orders
            .FirstOrDefaultAsync(order => order.Id == orderId, cancellationToken);

        if (localOrder is null || !ShouldApplyDelete(localOrder, deletedAtUtc))
        {
            return;
        }

        var localPayments = await _localDbContext.Payments
            .Where(payment => payment.OrderId == orderId)
            .ToListAsync(cancellationToken);

        if (localPayments.Count > 0)
        {
            _localDbContext.Payments.RemoveRange(localPayments);
        }

        _localDbContext.Orders.Remove(localOrder);
    }

    private async Task DeleteLocalPaymentAsync(
        Guid paymentId,
        DateTimeOffset deletedAtUtc,
        CancellationToken cancellationToken)
    {
        var localPayment = await _localDbContext.Payments
            .FirstOrDefaultAsync(payment => payment.Id == paymentId, cancellationToken);

        if (localPayment is null || !ShouldApplyDelete(localPayment, deletedAtUtc))
        {
            return;
        }

        _localDbContext.Payments.Remove(localPayment);
    }

    private async Task<SyncPullCursor?> GetExistingCursorAsync(string scope, CancellationToken cancellationToken)
    {
        var trackedCursor = _localDbContext.SyncPullCursors.Local
            .FirstOrDefault(item => item.Scope == scope);

        if (trackedCursor is not null)
        {
            return trackedCursor;
        }

        var cursor = await _localDbContext.SyncPullCursors
            .FirstOrDefaultAsync(item => item.Scope == scope, cancellationToken);

        if (cursor is not null)
        {
            return cursor;
        }

        return null;
    }

    private static async Task<List<TEntity>> GetRemoteBatchAsync<TEntity>(
        DbSet<TEntity> remoteSet,
        DateTimeOffset? lastSyncedAtUtc,
        int maxItems,
        CancellationToken cancellationToken)
        where TEntity : AuditableEntity
    {
        var orderedCandidates = (await remoteSet
            .AsNoTracking()
            .ToListAsync(cancellationToken))
            .Where(entity => lastSyncedAtUtc is null || entity.UpdatedAtUtc > lastSyncedAtUtc.Value)
            .OrderBy(entity => entity.UpdatedAtUtc)
            .ThenBy(entity => entity.Id)
            .ToList();

        if (orderedCandidates.Count == 0 || orderedCandidates.Count <= maxItems)
        {
            return orderedCandidates;
        }

        var boundaryTimestampUtc = orderedCandidates[maxItems - 1].UpdatedAtUtc;
        var entitiesBeforeBoundary = orderedCandidates
            .Where(entity => entity.UpdatedAtUtc < boundaryTimestampUtc)
            .ToList();

        var allBoundaryEntities = orderedCandidates
            .Where(entity => entity.UpdatedAtUtc == boundaryTimestampUtc)
            .ToList();

        entitiesBeforeBoundary.AddRange(allBoundaryEntities);
        return entitiesBeforeBoundary;
    }

    private bool ShouldApplyIncomingChange(AuditableEntity localEntity, AuditableEntity remoteEntity)
    {
        return _syncConflictResolver.ShouldApplyRemote(localEntity.UpdatedAtUtc, remoteEntity.UpdatedAtUtc);
    }

    private bool ShouldApplyDelete(AuditableEntity localEntity, DateTimeOffset deletedAtUtc)
    {
        return _syncConflictResolver.ShouldApplyRemote(localEntity.UpdatedAtUtc, deletedAtUtc);
    }

    private async Task<List<SyncDeletionTombstone>> GetDeletionTombstoneBatchAsync(
        DateTimeOffset? lastSyncedAtUtc,
        int maxItems,
        CancellationToken cancellationToken)
    {
        var orderedCandidates = (await _centralDbContext.SyncDeletionTombstones
            .AsNoTracking()
            .ToListAsync(cancellationToken))
            .Where(item => lastSyncedAtUtc is null || item.DeletedAtUtc > lastSyncedAtUtc.Value)
            .OrderBy(item => item.DeletedAtUtc)
            .ThenBy(item => item.Id)
            .ToList();

        if (orderedCandidates.Count == 0 || orderedCandidates.Count <= maxItems)
        {
            return orderedCandidates;
        }

        var boundaryTimestampUtc = orderedCandidates[maxItems - 1].DeletedAtUtc;
        var itemsBeforeBoundary = orderedCandidates
            .Where(item => item.DeletedAtUtc < boundaryTimestampUtc)
            .ToList();

        itemsBeforeBoundary.AddRange(
            orderedCandidates.Where(item => item.DeletedAtUtc == boundaryTimestampUtc));

        return itemsBeforeBoundary;
    }

    private static string NormalizeEntityType(string entityType)
    {
        if (string.IsNullOrWhiteSpace(entityType))
        {
            return string.Empty;
        }

        var trimmed = entityType.Trim();
        var dotIndex = trimmed.LastIndexOf('.');
        var tail = dotIndex >= 0 ? trimmed[(dotIndex + 1)..] : trimmed;
        return tail.ToLowerInvariant();
    }

    private readonly record struct PullStageResult(int ProcessedCount, int AppliedCount)
    {
        public static PullStageResult Empty => new(0, 0);
    }
}
