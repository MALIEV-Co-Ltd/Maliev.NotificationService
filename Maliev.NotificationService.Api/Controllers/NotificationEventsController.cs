using Asp.Versioning;
using Maliev.Aspire.ServiceDefaults.Authorization;
using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.NotificationService.Api.Authorization;
using Maliev.NotificationService.Api.Consumers;
using Microsoft.AspNetCore.Mvc;

namespace Maliev.NotificationService.Api.Controllers;

/// <summary>
/// API controller for dispatching notification events through NotificationService.
/// </summary>
[ApiController]
[ApiVersion("1")]
[Route("notification/v{version:apiVersion}/events")]
public sealed class NotificationEventsController(NotificationEventConsumer consumer) : ControllerBase
{
    /// <summary>
    /// Dispatches a notification event through the same processing path used by RabbitMQ consumers.
    /// </summary>
    /// <param name="notificationEvent">The notification event to dispatch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An accepted response containing the processed event id.</returns>
    [HttpPost]
    [RequirePermission(NotificationPermissions.Send)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Dispatch(
        [FromBody] NotificationEvent notificationEvent,
        CancellationToken cancellationToken)
    {
        if (notificationEvent.MessageId == Guid.Empty || notificationEvent.Payload is null)
        {
            return BadRequest(new { error = "A notification event with message id and payload is required." });
        }

        await consumer.ProcessAsync(notificationEvent, headers: null, cancellationToken);
        return Accepted(new { messageId = notificationEvent.MessageId });
    }
}
