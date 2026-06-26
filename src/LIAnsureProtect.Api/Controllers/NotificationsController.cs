using LIAnsureProtect.Application.Common.Security;
using LIAnsureProtect.Application.Notifications.Commands.MarkNotificationRead;
using LIAnsureProtect.Application.Notifications.Queries.ListMyNotifications;
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
    public async Task<ActionResult<ListMyNotificationsResult>> List(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListMyNotificationsQuery(), cancellationToken);

        return Ok(result);
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
