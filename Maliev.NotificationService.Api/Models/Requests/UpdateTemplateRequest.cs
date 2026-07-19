using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Maliev.NotificationService.Api.Models.Requests;

/// <summary>
/// Request model for updating an existing notification template
/// </summary>
public partial class UpdateTemplateRequest : IValidatableObject
{
    /// <summary>
    /// Updated human-readable template name shown to employees
    /// </summary>
    [StringLength(160, ErrorMessage = "DisplayName must be 160 characters or less")]
    public string? DisplayName { get; set; }

    /// <summary>
    /// Updated template content with {{parameter}} placeholders
    /// </summary>
    [Required(ErrorMessage = "ContentTemplate is required")]
    [StringLength(5000, MinimumLength = 1, ErrorMessage = "ContentTemplate must be between 1 and 5000 characters")]
    public string ContentTemplate { get; set; } = string.Empty;

    /// <summary>
    /// Updated subject/title template with {{parameter}} placeholders
    /// </summary>
    [StringLength(500, ErrorMessage = "SubjectTemplate must be 500 characters or less")]
    public string? SubjectTemplate { get; set; }

    /// <summary>
    /// Updated active state for this template version
    /// </summary>
    public bool? IsActive { get; set; }

    /// <summary>
    /// Updated list of required parameter names
    /// </summary>
    [Required(ErrorMessage = "Parameters array is required (use empty array if no parameters)")]
    public string[] Parameters { get; set; } = Array.Empty<string>();

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex ParameterPlaceholderRegex();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
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
