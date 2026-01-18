using Asp.Versioning;
using Maliev.Aspire.ServiceDefaults.Authorization;
using Maliev.NotificationService.Api.Authorization;
using Maliev.NotificationService.Api.Extensions;
using Maliev.NotificationService.Api.Models.Requests;
using Maliev.NotificationService.Api.Models.Responses;
using Maliev.NotificationService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Maliev.NotificationService.Api.Controllers;

/// <summary>
/// API controller for managing notification templates
/// </summary>
[ApiController]
[ApiVersion("1")]
[Route("notification/v{version:apiVersion}/templates")]
[Authorize]
public class TemplatesController : ControllerBase
{
    private readonly NotificationDbContext _dbContext;
    private readonly ILogger<TemplatesController> _logger;

    public TemplatesController(
        NotificationDbContext dbContext,
        ILogger<TemplatesController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new notification template
    /// </summary>
    /// <param name="request">Template creation request</param>
    /// <returns>Created template response</returns>
    [HttpPost]
    [RequirePermission(NotificationPermissions.TemplatesCreate)]
    [ProducesResponseType(typeof(TemplateResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateTemplate([FromBody] CreateTemplateRequest request)
    {
        // Check if template with same key, version, language, and channel already exists
        var channelTypeString = request.ChannelType.ToString().ToLowerInvariant();
        var existingTemplate = await _dbContext.NotificationTemplates
            .FirstOrDefaultAsync(t =>
                t.TemplateKey == request.TemplateKey &&
                t.Version == request.Version &&
                t.Language == request.Language &&
                t.ChannelType == channelTypeString);

        if (existingTemplate != null)
        {
            return Conflict(new
            {
                error = "Template with the same key, version, language, and channel type already exists"
            });
        }

        // Create new template entity
        var template = request.ToEntity();

        // Add to database
        _dbContext.NotificationTemplates.Add(template);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Created template {TemplateKey} v{Version} for language {Language} and channel {ChannelType}",
            template.TemplateKey, template.Version, template.Language, template.ChannelType);

        var response = template.ToResponse();
        return CreatedAtAction(nameof(GetTemplate), new { id = template.Id }, response);
    }

    /// <summary>
    /// Gets a template by ID
    /// </summary>
    /// <param name="id">Template ID</param>
    /// <returns>Template response</returns>
    [HttpGet("{id}")]
    [RequirePermission(NotificationPermissions.TemplatesRead)]
    [ProducesResponseType(typeof(TemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTemplate(Guid id)
    {
        var template = await _dbContext.NotificationTemplates.FindAsync(id);

        if (template == null)
        {
            return NotFound(new { error = "Template not found" });
        }

        return Ok(template.ToResponse());
    }

    /// <summary>
    /// Updates an existing template
    /// </summary>
    /// <param name="id">Template ID</param>
    /// <param name="request">Template update request</param>
    /// <returns>Updated template response</returns>
    [HttpPut("{id}")]
    [RequirePermission(NotificationPermissions.TemplatesUpdate)]
    [ProducesResponseType(typeof(TemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTemplate(Guid id, [FromBody] UpdateTemplateRequest request)
    {
        var template = await _dbContext.NotificationTemplates.FindAsync(id);

        if (template == null)
        {
            return NotFound(new { error = "Template not found" });
        }

        // Update template fields
        request.ToEntity(template);

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Updated template {TemplateKey} v{Version} for language {Language} and channel {ChannelType}",
            template.TemplateKey, template.Version, template.Language, template.ChannelType);

        return Ok(template.ToResponse());
    }
}
