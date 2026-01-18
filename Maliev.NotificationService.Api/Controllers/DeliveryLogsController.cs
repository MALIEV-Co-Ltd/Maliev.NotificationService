using Asp.Versioning;
using Maliev.Aspire.ServiceDefaults.Authorization;
using Maliev.NotificationService.Api.Authorization;
using Maliev.NotificationService.Api.Extensions;
using Maliev.NotificationService.Api.Models.Responses;
using Maliev.NotificationService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Maliev.NotificationService.Api.Controllers;

/// <summary>
/// API controller for querying notification delivery logs.
/// Supports filtering by user, date range, status, and channel.
/// </summary>
[ApiController]
[ApiVersion("1")]
[Route("notification/v{version:apiVersion}/delivery-logs")]
[Authorize]
public class DeliveryLogsController : ControllerBase
{
    private readonly NotificationDbContext _dbContext;
    private readonly ILogger<DeliveryLogsController> _logger;

    public DeliveryLogsController(
        NotificationDbContext dbContext,
        ILogger<DeliveryLogsController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Gets delivery logs with optional filtering and pagination.
    /// </summary>
    /// <param name="userId">Filter by user ID</param>
    /// <param name="eventId">Filter by event ID</param>
    /// <param name="channelType">Filter by channel type (email, line, whatsapp, etc.)</param>
    /// <param name="status">Filter by delivery status (sent, delivered, failed, pending, rate_limited)</param>
    /// <param name="startDate">Filter by logs created after this date</param>
    /// <param name="endDate">Filter by logs created before this date</param>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Items per page (default: 20, max: 100)</param>
    /// <returns>Paginated delivery logs</returns>
    [HttpGet]
    [RequirePermission(NotificationPermissions.LogsRead)]
    [ProducesResponseType(typeof(PaginatedResponse<DeliveryLogResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaginatedResponse<DeliveryLogResponse>>> GetDeliveryLogs(
        [FromQuery] string? userId = null,
        [FromQuery] string? eventId = null,
        [FromQuery] string? channelType = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTimeOffset? startDate = null,
        [FromQuery] DateTimeOffset? endDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            // Self-service: Allow users to view their own logs
            var principalId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId != principalId && !User.HasClaim("permissions", NotificationPermissions.LogsRead))
            {
                return Forbid();
            }

            // Validate pagination parameters
            if (page < 1)
            {
                return BadRequest(new { error = "Page number must be 1 or greater" });
            }

            if (pageSize < 1 || pageSize > 100)
            {
                return BadRequest(new { error = "Page size must be between 1 and 100" });
            }

            // Build query with filters
            var query = _dbContext.DeliveryLogs.AsQueryable();

            if (!string.IsNullOrWhiteSpace(userId))
            {
                query = query.Where(l => l.UserId == userId);
            }

            if (!string.IsNullOrWhiteSpace(eventId))
            {
                query = query.Where(l => l.EventId == eventId);
            }

            if (!string.IsNullOrWhiteSpace(channelType))
            {
                query = query.Where(l => l.ChannelType == channelType.ToLower());
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(l => l.Status == status.ToLower());
            }

            if (startDate.HasValue)
            {
                query = query.Where(l => l.CreatedAt >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(l => l.CreatedAt <= endDate.Value);
            }

            // Order by most recent first
            query = query.OrderByDescending(l => l.CreatedAt);

            // Get total count for pagination
            var totalCount = await query.CountAsync();

            // Apply pagination
            var logs = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Map to response DTOs using extension method
            var responses = logs.Select(l => l.ToResponse()).ToList();

            var result = new PaginatedResponse<DeliveryLogResponse>
            {
                Items = responses,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };

            _logger.LogInformation(
                "Retrieved {Count} delivery logs (page {Page}/{TotalPages})",
                responses.Count,
                page,
                result.TotalPages);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving delivery logs");
            return StatusCode(500, new { error = "An error occurred while retrieving delivery logs" });
        }
    }

    /// <summary>
    /// Gets a specific delivery log by ID.
    /// </summary>
    /// <param name="id">Delivery log ID</param>
    /// <returns>Delivery log details</returns>
    [HttpGet("{id}")]
    [RequirePermission(NotificationPermissions.LogsRead)]
    [ProducesResponseType(typeof(DeliveryLogResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeliveryLogResponse>> GetDeliveryLogById(Guid id)
    {
        try
        {
            var log = await _dbContext.DeliveryLogs
                .FirstOrDefaultAsync(l => l.Id == id);

            if (log == null)
            {
                return NotFound(new { error = $"Delivery log with ID {id} not found" });
            }

            // Self-service logic
            var principalId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (log.UserId != principalId && !User.HasClaim("permissions", NotificationPermissions.LogsRead))
            {
                return Forbid();
            }

            return Ok(log.ToResponse());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving delivery log {Id}", id);
            return StatusCode(500, new { error = "An error occurred while retrieving the delivery log" });
        }
    }
}
