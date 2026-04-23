using STailor.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace STailor.Infrastructure.Persistence;

public sealed class LocalTailorDbContext : TailorDbContextBase
{
    public LocalTailorDbContext(DbContextOptions<LocalTailorDbContext> options)
        : base(options)
    {
    }

    public DbSet<SyncPullCursor> SyncPullCursors => Set<SyncPullCursor>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SyncPullCursor>(entity =>
        {
            entity.ToTable("sync_pull_cursors");
            entity.HasKey(cursor => cursor.Scope);
            entity.Property(cursor => cursor.Scope).HasMaxLength(64).IsRequired();
        });
    }
}
