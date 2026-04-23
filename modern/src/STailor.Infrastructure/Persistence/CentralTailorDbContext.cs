using STailor.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace STailor.Infrastructure.Persistence;

public sealed class CentralTailorDbContext : TailorDbContextBase
{
    public CentralTailorDbContext(DbContextOptions<CentralTailorDbContext> options)
        : base(options)
    {
    }

    public DbSet<SyncDeletionTombstone> SyncDeletionTombstones => Set<SyncDeletionTombstone>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SyncDeletionTombstone>(entity =>
        {
            entity.ToTable("sync_deletion_tombstones");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.EntityType).HasMaxLength(64).IsRequired();
            entity.HasIndex(item => new { item.EntityType, item.EntityId }).IsUnique();
            entity.HasIndex(item => item.DeletedAtUtc);
        });
    }
}
