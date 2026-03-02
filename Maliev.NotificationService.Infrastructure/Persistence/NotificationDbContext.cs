using Maliev.Aspire.ServiceDefaults.Database;
using Maliev.NotificationService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Maliev.NotificationService.Infrastructure.Persistence;

public class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options)
    {
    }

    public DbSet<DeliveryLog> DeliveryLogs => Set<DeliveryLog>();
    public DbSet<RetryQueueEntry> RetryQueueEntries => Set<RetryQueueEntry>();
    public DbSet<DeadLetterRecord> DeadLetterRecords => Set<DeadLetterRecord>();
    public DbSet<UserNotificationPreference> UserNotificationPreferences => Set<UserNotificationPreference>();
    public DbSet<ChannelBinding> ChannelBindings => Set<ChannelBinding>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();

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
        });

        modelBuilder.Entity<RetryQueueEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.EventId);
            entity.HasIndex(e => e.ScheduledTime);
        });

        modelBuilder.Entity<DeadLetterRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.EventId);
            entity.HasIndex(e => e.EscalationStatus);
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<UserNotificationPreference>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.HasIndex(e => e.PrimaryChannelType);
            entity.Property(e => e.FallbackChannelTypes)
                .HasColumnType("jsonb");
            entity.Property(e => e.OptOutCategories)
                .HasColumnType("jsonb");
        });

        modelBuilder.Entity<ChannelBinding>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.IsValid);
            entity.HasIndex(e => new { e.UserId, e.ChannelType }).IsUnique();
        });

        modelBuilder.Entity<NotificationTemplate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TemplateKey, e.Version, e.Language, e.ChannelType }).IsUnique();
            entity.HasIndex(e => e.TemplateKey);
        });

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
