using System.Security.Claims;
using HereAndNowService.DTOs;
using HereAndNowService.Mappers;
using HereAndNowService.Models.Exceptions;
using HereAndNowService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HereAndNowService.Controllers;

/// <summary>
/// Controller for Reminder management operations
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class RemindersController : ControllerBase
{
    private readonly ITaskReminderService _reminderService;
    private readonly ILogger<RemindersController> _logger;

    /// <summary>
    /// Creates a new RemindersController instance
    /// </summary>
    public RemindersController(ITaskReminderService reminderService, ILogger<RemindersController> logger)
    {
        _reminderService = reminderService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new reminder for a task
    /// </summary>
    /// <param name="createReminderDto">The reminder creation request</param>
    /// <returns>The created reminder</returns>
    /// <response code="201">Returns the newly created reminder</response>
    /// <response code="400">If the task already has a reminder</response>
    /// <response code="404">If the task is not found</response>
    /// <response code="401">If the user is not authenticated</response>
    [HttpPost]
    [ProducesResponseType(typeof(TaskReminderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TaskReminderDto>> CreateReminder([FromBody] CreateReminderDto createReminderDto)
    {
        var userId = GetUserId();

        _logger.LogInformation("Creating reminder for task {TaskId} by user {UserId}",
            createReminderDto.TaskId, userId);

        try
        {
            var reminder = await _reminderService.CreateReminderAsync(
                userId,
                createReminderDto.TaskId,
                createReminderDto.ScheduledTime);

            var reminderDto = ReminderMapper.ToDto(reminder);

            return CreatedAtAction(
                nameof(GetReminderById),
                new { id = reminderDto.Id },
                reminderDto);
        }
        catch (TaskNotFoundException)
        {
            return NotFound(CreateErrorResponse("TASK_NOT_FOUND",
                $"Task with ID {createReminderDto.TaskId} not found"));
        }
        catch (ReminderAlreadyExistsException)
        {
            return BadRequest(CreateErrorResponse("REMINDER_ALREADY_EXISTS",
                $"A reminder already exists for task with ID {createReminderDto.TaskId}"));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid reminder creation request");
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", ex.Message));
        }
    }

    /// <summary>
    /// Gets all non-dismissed reminders for the authenticated user
    /// </summary>
    /// <returns>List of reminders sorted by scheduled time</returns>
    /// <response code="200">Returns the list of reminders</response>
    /// <response code="401">If the user is not authenticated</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TaskReminderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<TaskReminderDto>>> GetReminders()
    {
        var userId = GetUserId();

        _logger.LogInformation("Getting reminders for user {UserId}", userId);

        var reminders = await _reminderService.GetRemindersAsync(userId);
        return Ok(ReminderMapper.ToDtoList(reminders));
    }

    /// <summary>
    /// Gets a specific reminder by ID
    /// </summary>
    /// <param name="id">The reminder ID</param>
    /// <returns>The requested reminder</returns>
    /// <response code="200">Returns the reminder</response>
    /// <response code="404">If the reminder is not found</response>
    /// <response code="401">If the user is not authenticated</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TaskReminderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TaskReminderDto>> GetReminderById(string id)
    {
        var userId = GetUserId();

        _logger.LogInformation("Getting reminder {ReminderId} for user {UserId}", id, userId);

        var reminder = await _reminderService.GetReminderByIdAsync(userId, id);

        if (reminder is null)
        {
            return NotFound(CreateErrorResponse("REMINDER_NOT_FOUND",
                $"Reminder with ID {id} not found"));
        }

        return Ok(ReminderMapper.ToDto(reminder));
    }

    /// <summary>
    /// Extracts the user ID from the JWT claims
    /// </summary>
    private string GetUserId()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            throw new UnauthorizedAccessException("User ID not found in token");
        }

        return userId;
    }

    /// <summary>
    /// Creates a standard error response
    /// </summary>
    private static ErrorResponseDto CreateErrorResponse(string code, string message)
    {
        return new ErrorResponseDto
        {
            Error = new ErrorDetailsDto
            {
                Code = code,
                Message = message
            }
        };
    }
}
