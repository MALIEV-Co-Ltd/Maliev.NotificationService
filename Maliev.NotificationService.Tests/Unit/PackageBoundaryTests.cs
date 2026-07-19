namespace Maliev.NotificationService.Tests.Unit;

/// <summary>
/// Protects package ownership across the API and infrastructure layers.
/// </summary>
public sealed class PackageBoundaryTests
{
    /// <summary>
    /// EF design-time tooling belongs only to the project that owns migrations.
    /// </summary>
    [Fact]
    public void EntityFrameworkDesignPackage_IsOwnedByInfrastructureOnly()
    {
        var root = FindRoot();
        var apiProject = File.ReadAllText(Path.Combine(
            root,
            "Maliev.NotificationService.Api",
            "Maliev.NotificationService.Api.csproj"));
        var infrastructureProject = File.ReadAllText(Path.Combine(
            root,
            "Maliev.NotificationService.Infrastructure",
            "Maliev.NotificationService.Infrastructure.csproj"));

        Assert.DoesNotContain("Microsoft.EntityFrameworkCore.Design", apiProject, StringComparison.Ordinal);
        Assert.Contains("Asp.Versioning.Http", apiProject, StringComparison.Ordinal);
        Assert.Contains("Microsoft.EntityFrameworkCore.Design", infrastructureProject, StringComparison.Ordinal);
    }

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Maliev.NotificationService.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate NotificationService repository root.");
    }
}
