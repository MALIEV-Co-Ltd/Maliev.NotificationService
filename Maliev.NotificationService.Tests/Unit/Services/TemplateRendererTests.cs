using Microsoft.Extensions.Caching.Memory;
using Maliev.NotificationService.Api.Services;
using Xunit;

namespace Maliev.NotificationService.Api.Tests.Unit.Services;

public class TemplateRendererTests : IDisposable
{
    private readonly ITemplateRenderer _templateRenderer;
    private readonly MemoryCache _memoryCache;

    public TemplateRendererTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _templateRenderer = new TemplateRenderer(_memoryCache);
    }

    public void Dispose()
    {
        _memoryCache.Dispose();
    }

    [Fact]
    public void Render_WithValidParameters_SubstitutesCorrectly()
    {
        // Arrange
        var template = "Hello {{name}}, your order #{{orderId}} has been confirmed!";
        var requiredParameters = new[] { "name", "orderId" };
        var parameters = new Dictionary<string, object>
        {
            { "name", "John Doe" },
            { "orderId", "12345" }
        };

        // Act
        var result = _templateRenderer.Render(template, requiredParameters, parameters);

        // Assert
        Assert.Equal("Hello John Doe, your order #12345 has been confirmed!", result);
    }

    [Fact]
    public void Render_WithMissingParameter_ThrowsTemplateRenderingException()
    {
        // Arrange
        var template = "Hello {{name}}, your order #{{orderId}} has been confirmed!";
        var requiredParameters = new[] { "name", "orderId" };
        var parameters = new Dictionary<string, object>
        {
            { "name", "John Doe" }
            // Missing orderId
        };

        // Act & Assert
        var exception = Assert.Throws<TemplateRenderingException>(() =>
            _templateRenderer.Render(template, requiredParameters, parameters));

        Assert.Contains("orderId", exception.Message);
    }

    [Fact]
    public void Render_WithMultipleMissingParameters_ThrowsWithAllMissingNames()
    {
        // Arrange
        var template = "Hello {{name}}, your order #{{orderId}} totaling {{amount}} has been confirmed!";
        var requiredParameters = new[] { "name", "orderId", "amount" };
        var parameters = new Dictionary<string, object>
        {
            { "name", "John Doe" }
            // Missing orderId and amount
        };

        // Act & Assert
        var exception = Assert.Throws<TemplateRenderingException>(() =>
            _templateRenderer.Render(template, requiredParameters, parameters));

        Assert.Contains("orderId", exception.Message);
        Assert.Contains("amount", exception.Message);
    }

    [Fact]
    public void Render_WithExtraParameters_IgnoresExtra()
    {
        // Arrange
        var template = "Hello {{name}}!";
        var requiredParameters = new[] { "name" };
        var parameters = new Dictionary<string, object>
        {
            { "name", "John Doe" },
            { "extraParam", "ignored" }
        };

        // Act
        var result = _templateRenderer.Render(template, requiredParameters, parameters);

        // Assert
        Assert.Equal("Hello John Doe!", result);
    }

    [Fact]
    public void Render_WithNumericParameters_ConvertsToString()
    {
        // Arrange
        var template = "Your balance is {{amount}} {{currency}}";
        var requiredParameters = new[] { "amount", "currency" };
        var parameters = new Dictionary<string, object>
        {
            { "amount", 1234.56 },
            { "currency", "USD" }
        };

        // Act
        var result = _templateRenderer.Render(template, requiredParameters, parameters);

        // Assert
        Assert.Equal("Your balance is 1234.56 USD", result);
    }

    [Fact]
    public void Render_WithSameParameterMultipleTimes_SubstitutesAll()
    {
        // Arrange
        var template = "{{name}} ordered item {{itemId}}. Thank you {{name}}!";
        var requiredParameters = new[] { "name", "itemId" };
        var parameters = new Dictionary<string, object>
        {
            { "name", "Alice" },
            { "itemId", "PROD-123" }
        };

        // Act
        var result = _templateRenderer.Render(template, requiredParameters, parameters);

        // Assert
        Assert.Equal("Alice ordered item PROD-123. Thank you Alice!", result);
    }

    [Fact]
    public void Render_WithEmptyTemplate_ReturnsEmpty()
    {
        // Arrange
        var template = "";
        var requiredParameters = Array.Empty<string>();
        var parameters = new Dictionary<string, object>();

        // Act
        var result = _templateRenderer.Render(template, requiredParameters, parameters);

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public void Render_WithNoParameters_ReturnsTemplateAsIs()
    {
        // Arrange
        var template = "This is a static message";
        var requiredParameters = Array.Empty<string>();
        var parameters = new Dictionary<string, object>();

        // Act
        var result = _templateRenderer.Render(template, requiredParameters, parameters);

        // Assert
        Assert.Equal("This is a static message", result);
    }

    [Fact]
    public void Render_CachesRenderedTemplate_ForSameParameterHash()
    {
        // Arrange
        var template = "Hello {{name}}, order #{{orderId}}";
        var requiredParameters = new[] { "name", "orderId" };
        var parameters = new Dictionary<string, object>
        {
            { "name", "Bob" },
            { "orderId", "9999" }
        };

        // Act - render twice with same parameters
        var result1 = _templateRenderer.Render(template, requiredParameters, parameters);
        var result2 = _templateRenderer.Render(template, requiredParameters, parameters);

        // Assert - should return same cached result
        Assert.Equal(result1, result2);
        Assert.Equal("Hello Bob, order #9999", result1);
    }

    [Fact]
    public void Render_WithSpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        var template = "Email: {{email}}, Amount: ${{amount}}";
        var requiredParameters = new[] { "email", "amount" };
        var parameters = new Dictionary<string, object>
        {
            { "email", "test@example.com" },
            { "amount", "99.99" }
        };

        // Act
        var result = _templateRenderer.Render(template, requiredParameters, parameters);

        // Assert
        Assert.Equal("Email: test@example.com, Amount: $99.99", result);
    }
}
