using LIAnsureProtect.Application.Common.Security;
using LIAnsureProtect.Modules.Notifications.Application.Commands.MarkNotificationRead;
using LIAnsureProtect.Modules.Notifications.Application.Commands.AcknowledgeNotificationSubject;
using LIAnsureProtect.Modules.Notifications.Application.Queries.ListMyNotifications;
using LIAnsureProtect.Modules.Notifications.Application.Queries.GetUnreadNotificationCount;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LIAnsureProtect.Api.Controllers;

[ApiController]
[Route("api/v1/notifications")]
[Authorize(Policy = ApplicationPolicies.ReadNotifications)]
public sealed class NotificationsController(ISender sender) : ControllerBase
{
    [HttpGet("unread-count")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<GetUnreadNotificationCountResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<GetUnreadNotificationCountResult>> GetUnreadCount(
        CancellationToken cancellationToken)
    {
        return Ok(await sender.Send(new GetUnreadNotificationCountQuery(), cancellationToken));
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ListMyNotificationsResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ListMyNotificationsResult>> List(
        [FromQuery] string? search,
        [FromQuery] string? type,
        [FromQuery] bool? isUnread,
        [FromQuery] string? scope,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(
                new ListMyNotificationsQuery(search, type, isUnread, scope),
                cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Notification filters are invalid.",
                Detail = exception.Message
            });
        }
    }

    [HttpPost("{notificationId:guid}/read")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkRead(
        Guid notificationId,
        CancellationToken cancellationToken)
    {
        var marked = await sender.Send(
            new MarkNotificationReadCommand(notificationId),
            cancellationToken);

        return marked ? Ok() : NotFound();
    }

    [HttpPost("subjects/{subjectReferenceType}/{subjectReferenceId}/view")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AcknowledgeSubject(
        string subjectReferenceType,
        string subjectReferenceId,
        [FromQuery] string scope = "personal",
        CancellationToken cancellationToken = default)
    {
        try
        {
            await sender.Send(
                new AcknowledgeNotificationSubjectCommand(
                    subjectReferenceType,
                    subjectReferenceId,
                    scope),
                cancellationToken);
            return NoContent();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Notification subject is invalid.",
                Detail = exception.Message
            });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}
