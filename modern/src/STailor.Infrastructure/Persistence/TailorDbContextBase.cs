using Microsoft.EntityFrameworkCore;
using STailor.Core.Domain.Entities;
using STailor.Core.Domain.Enums;

namespace STailor.Infrastructure.Persistence;

public abstract class TailorDbContextBase : DbContext
{
    protected TailorDbContextBase(DbContextOptions options)
        : base(options)
    {
    }

    public DbSet<CustomerProfile> CustomerProfiles => Set<CustomerProfile>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<SyncQueueItem> SyncQueueItems => Set<SyncQueueItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CustomerProfile>(entity =>
        {
            entity.ToTable("customer_profiles");
            entity.HasKey(profile => profile.Id);
            entity.Property(profile => profile.FullName).HasMaxLength(120).IsRequired();
            entity.Property(profile => profile.PhoneNumber).HasMaxLength(30).IsRequired();
            entity.Property(profile => profile.City).HasMaxLength(120).IsRequired();
            entity.Property(profile => profile.Notes).HasMaxLength(500);
            entity.Property(profile => profile.BaselineMeasurementsJson).HasColumnType("text").IsRequired();
            entity.Property(profile => profile.CreatedBy).HasMaxLength(80).IsRequired();
            entity.Property(profile => profile.ModifiedBy).HasMaxLength(80).IsRequired();
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("orders");
            entity.HasKey(order => order.Id);
            entity.Property(order => order.GarmentType).HasMaxLength(80).IsRequired();
            entity.Property(order => order.MeasurementSnapshotJson).HasColumnType("text").IsRequired();
            entity.Property(order => order.PhotoAttachmentsJson).HasColumnType("text").IsRequired();
            entity.Property(order => order.TrialScheduleStatus).HasMaxLength(32);
            entity.Property(order => order.Status)
                .HasConversion(
                    status => status.ToString(),
                    status => Enum.Parse<OrderStatus>(status))
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(order => order.AmountCharged).HasPrecision(18, 2).IsRequired();
            entity.Property(order => order.AmountPaid).HasPrecision(18, 2).IsRequired();
            entity.Property(order => order.CreatedBy).HasMaxLength(80).IsRequired();
            entity.Property(order => order.ModifiedBy).HasMaxLength(80).IsRequired();

            entity
                .HasOne<CustomerProfile>()
                .WithMany()
                .HasForeignKey(order => order.CustomerProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("payments");
            entity.HasKey(payment => payment.Id);
            entity.Property(payment => payment.Amount).HasPrecision(18, 2).IsRequired();
            entity.Property(payment => payment.Note).HasMaxLength(300);
            entity.Property(payment => payment.CreatedBy).HasMaxLength(80).IsRequired();
            entity.Property(payment => payment.ModifiedBy).HasMaxLength(80).IsRequired();

            entity
                .HasOne(payment => payment.Order)
                .WithMany(order => order.Payments)
                .HasForeignKey(payment => payment.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SyncQueueItem>(entity =>
        {
            entity.ToTable("sync_queue_items");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.EntityType).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Operation).HasMaxLength(32).IsRequired();
            entity.Property(item => item.IdempotencyKey).HasMaxLength(220).IsRequired();
            entity.Property(item => item.PayloadJson).HasColumnType("text").IsRequired();
            entity.Property(item => item.LastError).HasMaxLength(500);
            entity.Property(item => item.Status)
                .HasConversion(
                    status => status.ToString(),
                    status => Enum.Parse<SyncQueueStatus>(status))
                .HasMaxLength(16)
                .IsRequired();
            entity.Property(item => item.CreatedBy).HasMaxLength(80).IsRequired();
            entity.Property(item => item.ModifiedBy).HasMaxLength(80).IsRequired();

            entity.HasIndex(item => item.Status);
            entity.HasIndex(item => item.EnqueuedAtUtc);
            entity.HasIndex(item => item.NextAttemptAtUtc);
            entity.HasIndex(item => item.IdempotencyKey).IsUnique();
        });

        base.OnModelCreating(modelBuilder);
    }
}
