using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Maliev.NotificationService.Api.Services;

/// <summary>
/// Exception thrown when template rendering fails due to missing or invalid parameters
/// </summary>
public partial class TemplateRenderingException : Exception
{
    public TemplateRenderingException(string message) : base(message)
    {
    }
}

/// <summary>
/// Service for rendering notification templates with parameter substitution and caching
/// </summary>
public partial class TemplateRenderer : ITemplateRenderer
{
    private readonly IMemoryCache _cache;

    public TemplateRenderer(IMemoryCache cache)
    {
        _cache = cache;
    }

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex ParameterPlaceholderRegex();

    public string Render(string template, string[] requiredParameters, Dictionary<string, object> parameters)
    {
        // Validate that all required parameters are present
        var missingParameters = requiredParameters.Except(parameters.Keys).ToList();
        if (missingParameters.Count != 0)
        {
            throw new TemplateRenderingException(
                $"Missing required parameters: {string.Join(", ", missingParameters)}");
        }

        // Generate cache key from template and parameters
        var cacheKey = $"tpl_render:{GenerateCacheKey(template, parameters)}";

        // Return cached result if available
        if (_cache.TryGetValue(cacheKey, out string? cachedResult) && cachedResult != null)
        {
            return cachedResult;
        }

        // Perform parameter substitution using Regex.Replace
        var result = ParameterPlaceholderRegex().Replace(template, match =>
        {
            var paramName = match.Groups[1].Value;
            if (parameters.TryGetValue(paramName, out var value))
            {
                return value?.ToString() ?? string.Empty;
            }
            return match.Value; // Keep placeholder if parameter not found
        });

        // Cache the result with size limit and sliding expiration to prevent memory leaks
        _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
        {
            Size = 1,
            SlidingExpiration = TimeSpan.FromHours(1)
        });

        return result;
    }

    /// <summary>
    /// Generates a cache key from template and parameter values using SHA256 hash
    /// </summary>
    private static string GenerateCacheKey(string template, Dictionary<string, object> parameters)
    {
        var sb = new StringBuilder();
        sb.Append(template);
        foreach (var kvp in parameters.OrderBy(p => p.Key))
        {
            sb.Append('|');
            sb.Append(kvp.Key);
            sb.Append('=');
            sb.Append(System.Text.Json.JsonSerializer.Serialize(kvp.Value));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}
