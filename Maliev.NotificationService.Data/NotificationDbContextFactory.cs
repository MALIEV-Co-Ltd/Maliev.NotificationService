using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Maliev.NotificationService.Data;

/// <summary>
/// Design-time factory for creating NotificationDbContext instances during migrations.
/// </summary>
public class NotificationDbContextFactory : IDesignTimeDbContextFactory<NotificationDbContext>
{
    /// <summary>
    /// Creates a new instance of NotificationDbContext for design-time operations.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <returns>A new NotificationDbContext instance.</returns>
    public NotificationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<NotificationDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=notification_design;Username=postgres;Password=postgres");
        return new NotificationDbContext(optionsBuilder.Options);
    }
}
