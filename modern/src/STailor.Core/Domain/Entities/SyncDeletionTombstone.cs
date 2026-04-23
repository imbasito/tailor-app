using STailor.Core.Domain.Exceptions;

namespace STailor.Core.Domain.Entities;

public sealed class SyncDeletionTombstone
{
    private SyncDeletionTombstone()
    {
    }

    public SyncDeletionTombstone(string entityType, Guid entityId, DateTimeOffset deletedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(entityType))
        {
            throw new DomainRuleViolationException("Deletion tombstone entity type is required.");
        }

        if (entityId == Guid.Empty)
        {
            throw new DomainRuleViolationException("Deletion tombstone entity id is required.");
        }

        EntityType = entityType.Trim();
        EntityId = entityId;
        DeletedAtUtc = deletedAtUtc;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public string EntityType { get; private set; } = string.Empty;

    public Guid EntityId { get; private set; }

    public DateTimeOffset DeletedAtUtc { get; private set; }
}
