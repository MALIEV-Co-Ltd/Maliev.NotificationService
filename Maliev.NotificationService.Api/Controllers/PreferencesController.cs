using Asp.Versioning;
using Maliev.Aspire.ServiceDefaults.Authorization;
using Maliev.NotificationService.Api.Authorization;
using Maliev.NotificationService.Api.Extensions;
using Maliev.NotificationService.Api.Models.Requests;
using Maliev.NotificationService.Api.Models.Responses;
using Maliev.NotificationService.Domain.Entities;
using Maliev.NotificationService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Maliev.NotificationService.Api.Controllers;

/// <summary>
/// API controller for managing user notification preferences
/// </summary>
[ApiController]
[ApiVersion("1")]
[Route("notification/v{version:apiVersion}/preferences")]
public class PreferencesController : ControllerBase
{
    private readonly NotificationDbContext _dbContext;
    private readonly ILogger<PreferencesController> _logger;

    public PreferencesController(
        NotificationDbContext dbContext,
        ILogger<PreferencesController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Create user notification preferences
    /// </summary>
    /// <param name="request">Preference creation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created preference response</returns>
    [HttpPost]
    [RequirePermission(NotificationPermissions.PreferencesUpdate)]
    [ProducesResponseType(typeof(PreferenceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreatePreferences(
        [FromBody] CreatePreferenceRequest request,
        CancellationToken cancellationToken)
    {
        // Self-service: Allow users to create preferences for themselves
        var principalId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (principalId != request.UserId && !HasPermission(NotificationPermissions.PreferencesUpdate))
        {
            return Forbid();
        }

        // Check if preferences already exist
        var existing = await _dbContext.UserNotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == request.UserId, cancellationToken);

        if (existing != null)
        {
            _logger.LogWarning("Preferences already exist for user: {UserId}", request.UserId);
            return Conflict(new
            {
                error = "PREFERENCES_EXIST",
                message = $"Preferences already exist for user {request.UserId}"
            });
        }

        // Create new preferences
        var preference = request.ToEntity();

        _dbContext.UserNotificationPreferences.Add(preference);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created preferences for user: {UserId}", request.UserId);

        var response = preference.ToResponse();
        var apiVersion = HttpContext.RequestedApiVersion?.ToString() ?? "1.0";
        return CreatedAtAction(
            nameof(GetPreferences),
            new { userId = preference.UserId, version = apiVersion },
            response);
    }

    /// <summary>
    /// Get user notification preferences
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Preference response</returns>
    [HttpGet("{userId}")]
    [RequirePermission(NotificationPermissions.PreferencesRead)]
    [ProducesResponseType(typeof(PreferenceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPreferences(
        string userId,
        CancellationToken cancellationToken)
    {
        // Self-service: Allow users to view their own preferences
        var principalId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (principalId != userId && !HasPermission(NotificationPermissions.PreferencesReadAny))
        {
            return Forbid();
        }

        var preference = await _dbContext.UserNotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (preference == null)
        {
            _logger.LogInformation("Preferences not found for user: {UserId}", userId);
            return NotFound(new
            {
                error = "PREFERENCES_NOT_FOUND",
                message = $"Preferences not found for user {userId}"
            });
        }

        var response = preference.ToResponse();
        return Ok(response);
    }

    /// <summary>
    /// Update user notification preferences
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="request">Preference update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated preference response</returns>
    [HttpPut("{userId}")]
    [RequirePermission(NotificationPermissions.PreferencesUpdate)]
    [ProducesResponseType(typeof(PreferenceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePreferences(
        string userId,
        [FromBody] UpdatePreferenceRequest request,
        CancellationToken cancellationToken)
    {
        // Self-service logic
        var principalId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId != principalId && !HasPermission(NotificationPermissions.PreferencesUpdate))
        {
            return Forbid();
        }

        var preference = await _dbContext.UserNotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (preference == null)
        {
            _logger.LogInformation("Preferences not found for user: {UserId}", userId);
            return NotFound(new
            {
                error = "PREFERENCES_NOT_FOUND",
                message = $"Preferences not found for user {userId}"
            });
        }

        // Apply updates
        preference.ApplyUpdate(request);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated preferences for user: {UserId}", userId);

        var response = preference.ToResponse();
        return Ok(response);
    }

    /// <summary>
    /// Delete user notification preferences
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content</returns>
    [HttpDelete("{userId}")]
    [RequirePermission(NotificationPermissions.PreferencesDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePreferences(
        string userId,
        CancellationToken cancellationToken)
    {
        // Self-service logic
        var principalId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId != principalId && !HasPermission(NotificationPermissions.PreferencesDelete))
        {
            return Forbid();
        }

        var preference = await _dbContext.UserNotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (preference == null)
        {
            _logger.LogInformation("Preferences not found for user: {UserId}", userId);
            return NotFound(new
            {
                error = "PREFERENCES_NOT_FOUND",
                message = $"Preferences not found for user {userId}"
            });
        }

        _dbContext.UserNotificationPreferences.Remove(preference);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted preferences for user: {UserId}", userId);

        return NoContent();
    }

    private bool HasPermission(string permission)
    {
        return User.HasClaim("permissions", "*")
            || User.HasClaim("permissions", permission);
    }
}
