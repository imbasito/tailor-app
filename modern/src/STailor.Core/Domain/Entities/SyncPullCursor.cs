using STailor.Core.Domain.Exceptions;

namespace STailor.Core.Domain.Entities;

public sealed class SyncPullCursor
{
    private SyncPullCursor()
    {
    }

    public SyncPullCursor(string scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            throw new DomainRuleViolationException("Sync pull cursor scope is required.");
        }

        Scope = scope.Trim();
    }

    public string Scope { get; private set; } = string.Empty;

    public DateTimeOffset? LastSyncedAtUtc { get; private set; }

    public void Advance(DateTimeOffset lastSyncedAtUtc)
    {
        if (LastSyncedAtUtc is not null && lastSyncedAtUtc < LastSyncedAtUtc.Value)
        {
            throw new DomainRuleViolationException("Sync pull cursor cannot move backwards.");
        }

        LastSyncedAtUtc = lastSyncedAtUtc;
    }
}
