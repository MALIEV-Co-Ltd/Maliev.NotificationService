using Maliev.NotificationService.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Maliev.Aspire.ServiceDefaults.Database;

namespace Maliev.NotificationService.Data;

public class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options)
    {
    }

    // DbSets for User Story 1 (Phase 3)
    public DbSet<DeliveryLog> DeliveryLogs => Set<DeliveryLog>();
    public DbSet<RetryQueueEntry> RetryQueueEntries => Set<RetryQueueEntry>();
    public DbSet<DeadLetterRecord> DeadLetterRecords => Set<DeadLetterRecord>();

    // DbSets for User Story 2 (Phase 4)
    public DbSet<UserNotificationPreference> UserNotificationPreferences => Set<UserNotificationPreference>();
    public DbSet<ChannelBinding> ChannelBindings => Set<ChannelBinding>();

    // DbSets for User Story 3 (Phase 5)
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // DeliveryLog configuration
        modelBuilder.Entity<DeliveryLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.EventId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.CreatedAt); // For time-range queries and partitioning
            entity.HasIndex(e => e.Status);
        });

        // RetryQueueEntry configuration
        modelBuilder.Entity<RetryQueueEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.EventId);
            entity.HasIndex(e => e.ScheduledTime); // Critical for picking due retries
        });

        // DeadLetterRecord configuration
        modelBuilder.Entity<DeadLetterRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.EventId);
            entity.HasIndex(e => e.EscalationStatus);
            entity.HasIndex(e => e.CreatedAt);
        });

        // UserNotificationPreference configuration
        modelBuilder.Entity<UserNotificationPreference>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.HasIndex(e => e.PrimaryChannelType); // For analytics queries
        });

        // ChannelBinding configuration
        modelBuilder.Entity<ChannelBinding>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.IsValid); // For filtering active bindings
            entity.HasIndex(e => new { e.UserId, e.ChannelType }).IsUnique(); // One binding per user per channel
        });

        // NotificationTemplate configuration
        modelBuilder.Entity<NotificationTemplate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TemplateKey, e.Version, e.Language, e.ChannelType }).IsUnique(); // Unique template per key/version/language/channel
            entity.HasIndex(e => e.TemplateKey); // For lookups by template key
        });

        // Apply PostgreSQL snake_case naming convention globally
        SnakeCaseNamingHelper.ApplySnakeCaseNaming(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Update timestamps automatically for BaseEntity
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

        // Update timestamps for UserNotificationPreference (doesn't inherit from BaseEntity)
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

