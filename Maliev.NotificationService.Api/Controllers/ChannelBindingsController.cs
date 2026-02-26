using Asp.Versioning;
using Maliev.Aspire.ServiceDefaults.Authorization;
using Maliev.NotificationService.Api.Authorization;
using Maliev.NotificationService.Api.Extensions;
using Maliev.NotificationService.Api.Models.Requests;
using Maliev.NotificationService.Api.Models.Responses;
using Maliev.NotificationService.Api.Services;
using Maliev.NotificationService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Maliev.NotificationService.Api.Controllers;

/// <summary>
/// API controller for managing channel bindings
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("notification/v{version:apiVersion}/channel-bindings")]
public class ChannelBindingsController : ControllerBase
{
    private readonly NotificationDbContext _dbContext;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<ChannelBindingsController> _logger;

    public ChannelBindingsController(
        NotificationDbContext dbContext,
        IEncryptionService encryptionService,
        ILogger<ChannelBindingsController> logger)
    {
        _dbContext = dbContext;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    /// <summary>
    /// Create a channel binding
    /// </summary>
    /// <param name="request">Channel binding creation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created channel binding response</returns>
    [HttpPost]
    [RequirePermission(NotificationPermissions.BindingsCreate)]
    [ProducesResponseType(typeof(ChannelBindingResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateChannelBinding(
        [FromBody] CreateChannelBindingRequest request,
        CancellationToken cancellationToken)
    {
        // Self-service: Allow users to create bindings for themselves
        var principalId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (principalId != request.UserId && !User.HasClaim("permissions", NotificationPermissions.BindingsCreate))
        {
            return Forbid();
        }

        // Check if binding already exists for this user and channel type
        var existing = await _dbContext.ChannelBindings
            .FirstOrDefaultAsync(
                b => b.UserId == request.UserId && b.ChannelType == request.ChannelType.ToLowerInvariant(),
                cancellationToken);

        if (existing != null)
        {
            _logger.LogWarning(
                "Channel binding already exists: UserId={UserId}, ChannelType={ChannelType}",
                request.UserId,
                request.ChannelType);

            return Conflict(new
            {
                error = "BINDING_EXISTS",
                message = $"Channel binding already exists for user {request.UserId} and channel {request.ChannelType}"
            });
        }

        // Create new binding with encrypted channel identifier
        var binding = request.ToEntity(_encryptionService);

        _dbContext.ChannelBindings.Add(binding);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created channel binding: Id={Id}, UserId={UserId}, ChannelType={ChannelType}",
            binding.Id,
            request.UserId,
            request.ChannelType);

        var response = binding.ToResponse(_encryptionService);
        var apiVersion = HttpContext.GetRequestedApiVersion()?.ToString() ?? "1.0";
        return CreatedAtAction(
            nameof(GetChannelBinding),
            new { id = binding.Id, version = apiVersion },
            response);
    }

    /// <summary>
    /// Get a channel binding by ID
    /// </summary>
    /// <param name="id">Channel binding ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Channel binding response</returns>
    [HttpGet("{id}")]
    [RequirePermission(NotificationPermissions.BindingsRead)]
    [ProducesResponseType(typeof(ChannelBindingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChannelBinding(
        Guid id,
        CancellationToken cancellationToken)
    {
        var binding = await _dbContext.ChannelBindings
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

        if (binding == null)
        {
            _logger.LogInformation("Channel binding not found: Id={Id}", id);
            return NotFound(new
            {
                error = "BINDING_NOT_FOUND",
                message = $"Channel binding not found with ID {id}"
            });
        }

        // Self-service logic
        var principalId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (binding.UserId != principalId && !User.HasClaim("permissions", NotificationPermissions.BindingsRead))
        {
            return Forbid();
        }

        var response = binding.ToResponse(_encryptionService);
        return Ok(response);
    }

    /// <summary>
    /// Get all channel bindings for a user
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="isValid">Optional filter by validity status</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of channel binding responses</returns>
    [HttpGet("user/{userId}")]
    [ProducesResponseType(typeof(List<ChannelBindingResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserChannelBindings(
        string userId,
        [FromQuery] bool? isValid,
        CancellationToken cancellationToken)
    {
        // Self-service: Allow users to view their own bindings without explicit permission
        var principalId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (principalId != userId && !User.HasClaim("permissions", NotificationPermissions.BindingsListUser))
        {
            return Forbid();
        }

        var query = _dbContext.ChannelBindings
            .Where(b => b.UserId == userId);

        if (isValid.HasValue)
        {
            query = query.Where(b => b.IsValid == isValid.Value);
        }

        var bindings = await query.ToListAsync(cancellationToken);

        var responses = bindings.Select(b => b.ToResponse(_encryptionService)).ToList();

        _logger.LogInformation(
            "Retrieved {Count} channel bindings for user: {UserId}",
            responses.Count,
            userId);

        return Ok(responses);
    }

    /// <summary>
    /// Update a channel binding
    /// </summary>
    /// <param name="id">Channel binding ID</param>
    /// <param name="request">Channel binding update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated channel binding response</returns>
    [HttpPut("{id}")]
    [RequirePermission(NotificationPermissions.BindingsUpdate)]
    [ProducesResponseType(typeof(ChannelBindingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateChannelBinding(
        Guid id,
        [FromBody] UpdateChannelBindingRequest request,
        CancellationToken cancellationToken)
    {
        var binding = await _dbContext.ChannelBindings
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

        if (binding == null)
        {
            _logger.LogInformation("Channel binding not found: Id={Id}", id);
            return NotFound(new
            {
                error = "BINDING_NOT_FOUND",
                message = $"Channel binding not found with ID {id}"
            });
        }

        // Self-service logic
        var principalId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (binding.UserId != principalId && !User.HasClaim("permissions", NotificationPermissions.BindingsUpdate))
        {
            return Forbid();
        }

        // Apply updates with encryption
        binding.ApplyUpdate(request, _encryptionService);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Updated channel binding: Id={Id}, IsValid={IsValid}",
            id,
            binding.IsValid);

        var response = binding.ToResponse(_encryptionService);
        return Ok(response);
    }

    /// <summary>
    /// Delete a channel binding
    /// </summary>
    /// <param name="id">Channel binding ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content</returns>
    [HttpDelete("{id}")]
    [RequirePermission(NotificationPermissions.BindingsDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteChannelBinding(
        Guid id,
        CancellationToken cancellationToken)
    {
        var binding = await _dbContext.ChannelBindings
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

        if (binding == null)
        {
            _logger.LogInformation("Channel binding not found: Id={Id}", id);
            return NotFound(new
            {
                error = "BINDING_NOT_FOUND",
                message = $"Channel binding not found with ID {id}"
            });
        }

        // Self-service logic
        var principalId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (binding.UserId != principalId && !User.HasClaim("permissions", NotificationPermissions.BindingsDelete))
        {
            return Forbid();
        }

        _dbContext.ChannelBindings.Remove(binding);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted channel binding: Id={Id}", id);

        return NoContent();
    }
}
