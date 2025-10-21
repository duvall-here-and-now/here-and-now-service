using App.Models;
using App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Controllers;

/// <summary>
/// Provides endpoints for retrieving messages with various authentication levels
/// </summary>
[ApiController]
[Route("api/messages")]
public class MessagesController : ControllerBase
{
    private readonly IMessageService _messageService;

    public MessagesController(IMessageService messageService)
    {
        _messageService = messageService;
    }

    /// <summary>
    /// Retrieves a public message that doesn't require authentication
    /// </summary>
    /// <returns>A public message accessible to all users</returns>
    /// <response code="200">Returns the public message</response>
    [HttpGet("public")]
    [ProducesResponseType(typeof(Message), StatusCodes.Status200OK)]
    public ActionResult<Message> GetPublicMessage()
    {
        return _messageService.GetPublicMessage();
    }

    /// <summary>
    /// Retrieves a protected message that requires authentication
    /// </summary>
    /// <returns>A protected message accessible only to authenticated users</returns>
    /// <response code="200">Returns the protected message</response>
    /// <response code="401">If the user is not authenticated</response>
    [HttpGet("protected")]
    [Authorize]
    [ProducesResponseType(typeof(Message), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<Message> GetProtectedMessage()
    {
        return _messageService.GetProtectedMessage();
    }

    /// <summary>
    /// Retrieves an admin message that requires authentication
    /// </summary>
    /// <returns>An admin message accessible only to authenticated users</returns>
    /// <response code="200">Returns the admin message</response>
    /// <response code="401">If the user is not authenticated</response>
    [HttpGet("admin")]
    [Authorize]
    [ProducesResponseType(typeof(Message), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<Message> GetAdminMessage()
    {
        return _messageService.GetAdminMessage();
    }
}
