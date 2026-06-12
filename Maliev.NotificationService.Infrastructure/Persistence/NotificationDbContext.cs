using Maliev.Aspire.ServiceDefaults.Database;
using Maliev.NotificationService.Domain.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Maliev.NotificationService.Infrastructure.Persistence;

public class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options)
    {
    }

    public virtual DbSet<DeliveryLog> DeliveryLogs => Set<DeliveryLog>();
    public virtual DbSet<RetryQueueEntry> RetryQueueEntries => Set<RetryQueueEntry>();
    public virtual DbSet<DeadLetterRecord> DeadLetterRecords => Set<DeadLetterRecord>();
    public virtual DbSet<UserNotificationPreference> UserNotificationPreferences => Set<UserNotificationPreference>();
    public virtual DbSet<ChannelBinding> ChannelBindings => Set<ChannelBinding>();
    public virtual DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public virtual DbSet<DeduplicationEntry> DeduplicationEntries => Set<DeduplicationEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DeliveryLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.EventId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => new { e.EventId, e.UserId })
                .IsUnique()
                .HasFilter("\"status\" = 'delivered'");

            entity.Property<uint>("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();
        });

        modelBuilder.Entity<RetryQueueEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.EventId);
            entity.HasIndex(e => e.ScheduledTime);

            entity.Property<uint>("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();
        });

        modelBuilder.Entity<DeadLetterRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.EventId);
            entity.HasIndex(e => e.EscalationStatus);
            entity.HasIndex(e => e.CreatedAt);

            entity.Property<uint>("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();
        });

        modelBuilder.Entity<UserNotificationPreference>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.HasIndex(e => e.PrimaryChannelType);
            entity.Property(e => e.FallbackChannelTypes)
                .HasColumnType("jsonb");
            entity.Property(e => e.OptOutCategories)
                .HasColumnType("jsonb");

            entity.Property<uint>("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();
        });

        modelBuilder.Entity<ChannelBinding>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.IsValid);
            entity.HasIndex(e => new { e.UserId, e.ChannelType }).IsUnique();

            entity.Property<uint>("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();
        });

        modelBuilder.Entity<DeduplicationEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.EventId).IsUnique();
            entity.HasIndex(e => e.ExpiresAt);

            entity.Property<uint>("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();
        });

        modelBuilder.Entity<NotificationTemplate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TemplateKey, e.Version, e.Language, e.ChannelType }).IsUnique();
            entity.HasIndex(e => e.TemplateKey);

            entity.Property<uint>("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();
        });

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        SnakeCaseNamingHelper.ApplySnakeCaseNaming(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var baseEntityEntries = ChangeTracker.Entries<BaseEntity>();
        foreach (var entry in baseEntityEntries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = DateTimeOffset.UtcNow;
                entry.Entity.UpdatedAt = DateTimeOffset.UtcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        var preferenceEntries = ChangeTracker.Entries<UserNotificationPreference>();
        foreach (var entry in preferenceEntries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = DateTimeOffset.UtcNow;
                entry.Entity.UpdatedAt = DateTimeOffset.UtcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
