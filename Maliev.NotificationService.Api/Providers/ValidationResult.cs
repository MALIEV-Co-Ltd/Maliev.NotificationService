namespace Maliev.NotificationService.Api.Providers;

public record ValidationResult
{
    public bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }

    public static ValidationResult Valid() => new() { IsValid = true };
    public static ValidationResult Invalid(string errorMessage) => new() { IsValid = false, ErrorMessage = errorMessage };
}
