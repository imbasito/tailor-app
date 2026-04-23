namespace STailor.Core.Common.Entities;

public abstract class AuditableEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();

    public DateTimeOffset CreatedAtUtc { get; protected set; }

    public DateTimeOffset UpdatedAtUtc { get; protected set; }

    public string CreatedBy { get; protected set; } = "system";

    public string ModifiedBy { get; protected set; } = "system";

    public void StampCreated(DateTimeOffset nowUtc, string userId)
    {
        var actor = string.IsNullOrWhiteSpace(userId) ? "system" : userId.Trim();
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
        CreatedBy = actor;
        ModifiedBy = actor;
    }

    public void StampUpdated(DateTimeOffset nowUtc, string userId)
    {
        UpdatedAtUtc = nowUtc;
        ModifiedBy = string.IsNullOrWhiteSpace(userId) ? "system" : userId.Trim();
    }
}
