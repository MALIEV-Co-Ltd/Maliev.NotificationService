using Maliev.NotificationService.Api.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Maliev.NotificationService.Api.Models.Requests;

/// <summary>
/// Request model for creating a new notification template
/// </summary>
public partial class CreateTemplateRequest : IValidatableObject
{
    /// <summary>
    /// Template key identifier (kebab-case format, e.g., "order-confirmed")
    /// </summary>
    [Required(ErrorMessage = "TemplateKey is required")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "TemplateKey must be between 3 and 100 characters")]
    public string TemplateKey { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable template name shown to employees
    /// </summary>
    [StringLength(160, ErrorMessage = "DisplayName must be 160 characters or less")]
    public string? DisplayName { get; set; }

    /// <summary>
    /// Template version number
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "Version must be at least 1")]
    public int Version { get; set; }

    /// <summary>
    /// Language code (ISO 639-1, e.g., "en", "th")
    /// </summary>
    [Required(ErrorMessage = "Language is required")]
    [RegularExpression("^[a-z]{2}$", ErrorMessage = "Language must be a valid ISO 639-1 code (e.g., 'en', 'th')")]
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// Channel type this template is designed for
    /// </summary>
    [Required(ErrorMessage = "ChannelType is required")]
    public ChannelType ChannelType { get; set; }

    /// <summary>
    /// Template content with {{parameter}} placeholders
    /// </summary>
    [Required(ErrorMessage = "ContentTemplate is required")]
    [StringLength(5000, MinimumLength = 1, ErrorMessage = "ContentTemplate must be between 1 and 5000 characters")]
    public string ContentTemplate { get; set; } = string.Empty;

    /// <summary>
    /// Optional subject/title template with {{parameter}} placeholders
    /// </summary>
    [StringLength(500, ErrorMessage = "SubjectTemplate must be 500 characters or less")]
    public string SubjectTemplate { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether the template can be selected for new notifications
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// List of required parameter names expected in the template
    /// </summary>
    [Required(ErrorMessage = "Parameters array is required (use empty array if no parameters)")]
    public string[] Parameters { get; set; } = Array.Empty<string>();

    [GeneratedRegex(@"^[a-z][a-z0-9-]*$")]
    private static partial Regex TemplateKeyRegex();

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex ParameterPlaceholderRegex();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Validate template_key format (kebab-case)
        if (!TemplateKeyRegex().IsMatch(TemplateKey))
        {
            yield return new ValidationResult(
                "TemplateKey must be in kebab-case format (lowercase letters, numbers, and hyphens only, starting with a letter)",
                new[] { nameof(TemplateKey) });
        }

        // Validate that all parameter placeholders in subject and body match the Parameters array.
        var placeholdersInTemplate = ParameterPlaceholderRegex()
            .Matches($"{SubjectTemplate}\n{ContentTemplate}")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToHashSet();

        var declaredParameters = Parameters.ToHashSet();

        // Check for undeclared parameters used in template
        var undeclaredParams = placeholdersInTemplate.Except(declaredParameters).ToList();
        if (undeclaredParams.Count != 0)
        {
            yield return new ValidationResult(
                $"Template contains undeclared parameters: {string.Join(", ", undeclaredParams)}. All parameters used in template must be declared in Parameters array.",
                new[] { nameof(ContentTemplate), nameof(Parameters) });
        }

        // Check for declared parameters not used in template
        var unusedParams = declaredParameters.Except(placeholdersInTemplate).ToList();
        if (unusedParams.Count != 0)
        {
            yield return new ValidationResult(
                $"Parameters array contains unused parameters: {string.Join(", ", unusedParams)}. Remove them or use them in the template.",
                new[] { nameof(Parameters) });
        }
    }
}
