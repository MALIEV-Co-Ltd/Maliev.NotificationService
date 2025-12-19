namespace Maliev.NotificationService.Api.Services;

/// <summary>
/// Service for rendering notification templates with parameter substitution
/// </summary>
public interface ITemplateRenderer
{
    /// <summary>
    /// Renders a template by substituting {{parameter}} placeholders with actual values
    /// </summary>
    /// <param name="template">Template content with {{parameter}} placeholders</param>
    /// <param name="requiredParameters">List of required parameter names</param>
    /// <param name="parameters">Dictionary of parameter values</param>
    /// <returns>Rendered template with all parameters substituted</returns>
    /// <exception cref="TemplateRenderingException">Thrown when required parameters are missing</exception>
    string Render(string template, string[] requiredParameters, Dictionary<string, object> parameters);
}
