using LIAnsureProtect.Application.Common.Security;
using LIAnsureProtect.Modules.Notifications.Application.Commands.MarkNotificationRead;
using LIAnsureProtect.Modules.Notifications.Application.Queries.ListMyNotifications;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LIAnsureProtect.Api.Controllers;

[ApiController]
[Route("api/v1/notifications")]
[Authorize(Policy = ApplicationPolicies.ReadNotifications)]
public sealed class NotificationsController(ISender sender) : ControllerBase
{
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
}
