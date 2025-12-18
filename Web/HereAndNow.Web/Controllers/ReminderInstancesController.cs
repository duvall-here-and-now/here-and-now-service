using System.Security.Claims;
using HereAndNowService.DTOs;
using HereAndNowService.Mappers;
using HereAndNowService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HereAndNowService.Controllers;

/// <summary>
/// Controller for managing reminder instances. All endpoints require authorization.
/// </summary>
[ApiController]
[Route("api/reminder-instances")]
[Authorize]
public class ReminderInstancesController : ControllerBase
{
    private readonly IReminderInstanceService _reminderInstanceService;
    private readonly ILogger<ReminderInstancesController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReminderInstancesController"/> class.
    /// </summary>
    /// <param name="reminderInstanceService">The reminder instance service.</param>
    /// <param name="logger">The logger instance.</param>
    public ReminderInstancesController(IReminderInstanceService reminderInstanceService, ILogger<ReminderInstancesController> logger)
    {
        _reminderInstanceService = reminderInstanceService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the authenticated user's ID from the JWT 'sub' claim.
    /// </summary>
    /// <returns>The user ID from the token.</returns>
    private string GetUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new InvalidOperationException("User ID not found in token");
        return userId;
    }

    /// <summary>
    /// Gets all reminder instances for the authenticated user.
    /// </summary>
    /// <returns>A collection of the user's reminder instances.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ReminderInstanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<IEnumerable<ReminderInstanceDto>> GetAll()
    {
        var userId = GetUserId();
        _logger.LogInformation("GET /api/reminder-instances - Request received to get all reminders for user: {UserId}", userId);
        var reminders = _reminderInstanceService.GetAll(userId);
        var dtos = ReminderInstanceMapper.ToDtos(reminders);
        _logger.LogInformation("GET /api/reminder-instances - Returning {Count} reminders with Status 200 OK", dtos.Count());
        return Ok(dtos);
    }

    /// <summary>
    /// Gets a specific reminder instance by ID for the authenticated user.
    /// </summary>
    /// <param name="id">The unique identifier of the reminder.</param>
    /// <returns>The reminder instance if found.</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ReminderInstanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ReminderInstanceDto> GetById(Guid id)
    {
        var userId = GetUserId();
        _logger.LogInformation("GET /api/reminder-instances/{ReminderId} - Request received for user: {UserId}", id, userId);
        var reminder = _reminderInstanceService.GetById(id, userId);

        if (reminder == null)
        {
            _logger.LogWarning("GET /api/reminder-instances/{ReminderId} - Reminder not found, returning 404 Not Found", id);
            return NotFound(new { message = $"Reminder with ID {id} not found." });
        }

        _logger.LogInformation("GET /api/reminder-instances/{ReminderId} - Returning reminder with Status 200 OK", id);
        return Ok(ReminderInstanceMapper.ToDto(reminder));
    }

    /// <summary>
    /// Creates a new reminder instance for the authenticated user.
    /// Server controls: Id, UserId, IsCompleted, IsDeleted, timestamps.
    /// </summary>
    /// <param name="request">The reminder creation request.</param>
    /// <returns>The created reminder instance.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ReminderInstanceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<ReminderInstanceDto> Create([FromBody] CreateReminderRequest request)
    {
        var userId = GetUserId();
        _logger.LogInformation("POST /api/reminder-instances - Request received to create new reminder for user: {UserId}", userId);

        var createdReminder = _reminderInstanceService.Create(
            userId,
            request.Text,
            request.ScheduledDateAndTime,
            request.ShouldPlaySound,
            request.ShouldDoVibration);

        var resultDto = ReminderInstanceMapper.ToDto(createdReminder);
        _logger.LogInformation("POST /api/reminder-instances - Successfully created reminder with ID: {ReminderId}, returning 201 Created", createdReminder.Id);

        return CreatedAtAction(
            nameof(GetById),
            new { id = resultDto.Id },
            resultDto
        );
    }

    /// <summary>
    /// Partially updates an existing reminder instance for the authenticated user.
    /// Only provided fields will be updated. Cannot modify state flags or timestamps.
    /// </summary>
    /// <param name="id">The unique identifier of the reminder to update.</param>
    /// <param name="request">The partial update request.</param>
    /// <returns>The updated reminder instance.</returns>
    [HttpPatch("{id}")]
    [ProducesResponseType(typeof(ReminderInstanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ReminderInstanceDto> Update(Guid id, [FromBody] UpdateReminderRequest request)
    {
        var userId = GetUserId();
        _logger.LogInformation("PATCH /api/reminder-instances/{ReminderId} - Request received to update reminder for user: {UserId}", id, userId);

        var updatedReminder = _reminderInstanceService.Update(
            id,
            userId,
            request.Text,
            request.ScheduledDateAndTime,
            request.ShouldPlaySound,
            request.ShouldDoVibration);

        if (updatedReminder == null)
        {
            _logger.LogWarning("PATCH /api/reminder-instances/{ReminderId} - Reminder not found, returning 404 Not Found", id);
            return NotFound(new { message = $"Reminder with ID {id} not found." });
        }

        _logger.LogInformation("PATCH /api/reminder-instances/{ReminderId} - Successfully updated reminder, returning 200 OK", id);
        return Ok(ReminderInstanceMapper.ToDto(updatedReminder));
    }

    /// <summary>
    /// Marks a reminder as completed for the authenticated user.
    /// Sets IsCompleted to true and records the completion timestamp.
    /// This operation is idempotent - completing an already completed reminder succeeds.
    /// </summary>
    /// <param name="id">The unique identifier of the reminder to complete.</param>
    /// <returns>The completed reminder instance.</returns>
    [HttpPost("{id}/complete")]
    [ProducesResponseType(typeof(ReminderInstanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public ActionResult<ReminderInstanceDto> Complete(Guid id)
    {
        var userId = GetUserId();
        _logger.LogInformation("POST /api/reminder-instances/{ReminderId}/complete - Request received for user: {UserId}", id, userId);

        try
        {
            var completedReminder = _reminderInstanceService.Complete(id, userId);

            if (completedReminder == null)
            {
                _logger.LogWarning("POST /api/reminder-instances/{ReminderId}/complete - Reminder not found, returning 404 Not Found", id);
                return NotFound(new { message = $"Reminder with ID {id} not found." });
            }

            _logger.LogInformation("POST /api/reminder-instances/{ReminderId}/complete - Successfully completed reminder, returning 200 OK", id);
            return Ok(ReminderInstanceMapper.ToDto(completedReminder));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("POST /api/reminder-instances/{ReminderId}/complete - Cannot complete deleted reminder, returning 409 Conflict", id);
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Soft-deletes a reminder instance for the authenticated user.
    /// Sets IsDeleted to true and records the deletion timestamp.
    /// This operation is idempotent - deleting an already deleted reminder succeeds.
    /// </summary>
    /// <param name="id">The unique identifier of the reminder to delete.</param>
    /// <returns>No content if successful.</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult Delete(Guid id)
    {
        var userId = GetUserId();
        _logger.LogInformation("DELETE /api/reminder-instances/{ReminderId} - Request received to delete reminder for user: {UserId}", id, userId);
        var deleted = _reminderInstanceService.Delete(id, userId);

        if (!deleted)
        {
            _logger.LogWarning("DELETE /api/reminder-instances/{ReminderId} - Reminder not found, returning 404 Not Found", id);
            return NotFound(new { message = $"Reminder with ID {id} not found." });
        }

        _logger.LogInformation("DELETE /api/reminder-instances/{ReminderId} - Successfully deleted reminder, returning 204 No Content", id);
        return NoContent();
    }
}
