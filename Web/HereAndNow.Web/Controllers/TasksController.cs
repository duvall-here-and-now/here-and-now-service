using System.Security.Claims;
using HereAndNowService.DTOs;
using HereAndNowService.Mappers;
using HereAndNowService.Models.Exceptions;
using HereAndNowService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HereAndNowService.Controllers;

/// <summary>
/// Controller for Task management operations
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly ITaskService _taskService;
    private readonly ILogger<TasksController> _logger;

    /// <summary>
    /// Creates a new TasksController instance
    /// </summary>
    public TasksController(ITaskService taskService, ILogger<TasksController> logger)
    {
        _taskService = taskService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new task, optionally with an associated reminder.
    /// This endpoint is deprecated - use POST /api/v1/commands with CreateTask command instead.
    /// </summary>
    /// <param name="createTaskDto">The task creation request with optional scheduledTime for reminder</param>
    /// <returns>The created task</returns>
    /// <response code="201">Returns the newly created task</response>
    /// <response code="400">If the request is invalid (including INVALID_SCHEDULED_TIME for past times)</response>
    /// <response code="401">If the user is not authenticated</response>
    [HttpPost]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [Obsolete("Use POST /api/v1/commands with CreateTask command instead")]
    public async Task<ActionResult<TaskDto>> CreateTask([FromBody] CreateTaskDto createTaskDto)
    {
        var userId = GetUserId();

        _logger.LogWarning("Legacy POST /api/v1/tasks endpoint used by user {UserId}. Migrate to POST /api/v1/commands with CreateTask command.",
            userId);

        _logger.LogInformation("Creating task for user {UserId} with reminder: {HasReminder}",
            userId, createTaskDto.ScheduledTime.HasValue);

        // Note: ScheduledTime validation (FutureTimeValidation) is handled by InvalidModelStateResponseFactory
        // which returns INVALID_SCHEDULED_TIME error code for scheduledTime validation failures

        try
        {
            var task = await _taskService.CreateTaskWithOptionalReminderAsync(
                createTaskDto.Name,
                userId,
                createTaskDto.ScheduledTime);

            var taskDto = TaskMapper.ToDto(task);

            return CreatedAtAction(
                nameof(GetTask),
                new { id = taskDto.Id },
                taskDto);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid task creation request");
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", ex.Message));
        }
    }

    /// <summary>
    /// Gets tasks for the authenticated user with optional sorting and pagination
    /// </summary>
    /// <param name="state">Optional filter by task state (OnDeck, InProgress, Completed)</param>
    /// <param name="orderBy">Field to order by (createdAt or completedAt). Default: createdAt</param>
    /// <param name="direction">Sort direction (asc or desc). Default: asc</param>
    /// <param name="skip">Number of items to skip for pagination. Default: 0</param>
    /// <param name="take">Number of items to return (max 100). Default: 50</param>
    /// <returns>Paginated list of tasks with metadata</returns>
    /// <response code="200">Returns the paginated list of tasks</response>
    /// <response code="400">If any query parameter is invalid</response>
    /// <response code="401">If the user is not authenticated</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedTasksDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedTasksDto>> GetTasks(
        [FromQuery] string? state = null,
        [FromQuery] string orderBy = "createdAt",
        [FromQuery] string direction = "asc",
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50)
    {
        var userId = GetUserId();

        _logger.LogInformation(
            "Getting tasks for user {UserId}: state={State}, orderBy={OrderBy}, direction={Direction}, skip={Skip}, take={Take}",
            userId, state ?? "all", orderBy, direction, skip, take);

        try
        {
            var pagedResult = await _taskService.GetTasksPagedAsync(userId, state, orderBy, direction, skip, take);
            return Ok(TaskMapper.ToPagedDto(pagedResult));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid task filter request");
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", ex.Message));
        }
    }

    /// <summary>
    /// Gets a specific task by ID
    /// </summary>
    /// <param name="id">The task ID</param>
    /// <returns>The requested task</returns>
    /// <response code="200">Returns the task</response>
    /// <response code="404">If the task is not found</response>
    /// <response code="401">If the user is not authenticated</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TaskDto>> GetTask(string id)
    {
        var userId = GetUserId();

        _logger.LogInformation("Getting task {TaskId} for user {UserId}", id, userId);

        try
        {
            var task = await _taskService.GetTaskByIdAsync(id, userId);
            return Ok(TaskMapper.ToDto(task));
        }
        catch (TaskNotFoundException)
        {
            return NotFound(CreateErrorResponse("TASK_NOT_FOUND", $"Task with ID {id} not found"));
        }
    }

    /// <summary>
    /// Updates an existing task
    /// </summary>
    /// <param name="id">The task ID</param>
    /// <param name="updateTaskDto">The update request with optional name and/or state</param>
    /// <returns>The updated task</returns>
    /// <response code="200">Returns the updated task</response>
    /// <response code="400">If the request is invalid</response>
    /// <response code="404">If the task is not found</response>
    /// <response code="401">If the user is not authenticated</response>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TaskDto>> UpdateTask(string id, [FromBody] UpdateTaskDto updateTaskDto)
    {
        var userId = GetUserId();

        _logger.LogInformation("Updating task {TaskId} for user {UserId}", id, userId);

        // Validate the DTO
        if (!updateTaskDto.IsValid(out var validationError))
        {
            _logger.LogWarning("Invalid update request for task {TaskId}: {Error}", id, validationError);
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", validationError!));
        }

        try
        {
            var task = await _taskService.UpdateTaskAsync(id, userId, updateTaskDto.Name, updateTaskDto.State);
            return Ok(TaskMapper.ToDto(task));
        }
        catch (TaskNotFoundException)
        {
            return NotFound(CreateErrorResponse("TASK_NOT_FOUND", $"Task with ID {id} not found"));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid update request for task {TaskId}", id);
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", ex.Message));
        }
    }

    /// <summary>
    /// Completes a task. If the task has an associated reminder, it will be atomically dismissed.
    /// This is the Task-Reminder Unity operation - the core experience of HereAndNow.
    /// </summary>
    /// <param name="id">The task ID to complete</param>
    /// <returns>The completed task with reminderId cleared (if reminder was dismissed)</returns>
    /// <response code="200">Returns the completed task</response>
    /// <response code="404">If the task is not found</response>
    /// <response code="500">If the Unity transaction fails</response>
    /// <response code="401">If the user is not authenticated</response>
    [HttpPut("{id}/complete")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TaskDto>> CompleteTask(string id)
    {
        var userId = GetUserId();

        _logger.LogInformation("Completing task {TaskId} with Unity for user {UserId}", id, userId);

        try
        {
            var task = await _taskService.CompleteTaskWithUnityAsync(userId, id);
            return Ok(TaskMapper.ToDto(task));
        }
        catch (TaskNotFoundException)
        {
            return NotFound(CreateErrorResponse("TASK_NOT_FOUND", $"Task with ID {id} not found"));
        }
        catch (UnityTransactionFailedException ex)
        {
            _logger.LogError(ex, "Unity transaction failed for task {TaskId}", id);
            return StatusCode(500, CreateErrorResponse(
                "UNITY_TRANSACTION_FAILED",
                "Failed to complete task and dismiss reminder. Please try again."));
        }
    }

    /// <summary>
    /// Deletes a task (soft-delete). If the task has an associated reminder, it will be atomically dismissed.
    /// This is the Task-Reminder Unity operation for deletion.
    /// </summary>
    /// <param name="id">The task ID to delete</param>
    /// <returns>No content on success</returns>
    /// <response code="204">Task successfully deleted</response>
    /// <response code="404">If the task is not found</response>
    /// <response code="500">If the Unity transaction fails</response>
    /// <response code="401">If the user is not authenticated</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteTask(string id)
    {
        var userId = GetUserId();

        _logger.LogInformation("Deleting task {TaskId} with Unity for user {UserId}", id, userId);

        try
        {
            await _taskService.DeleteTaskWithUnityAsync(userId, id);
            return NoContent();
        }
        catch (TaskNotFoundException)
        {
            return NotFound(CreateErrorResponse("TASK_NOT_FOUND", $"Task with ID {id} not found"));
        }
        catch (UnityTransactionFailedException ex)
        {
            _logger.LogError(ex, "Unity transaction failed for task deletion {TaskId}", id);
            return StatusCode(500, CreateErrorResponse(
                "UNITY_TRANSACTION_FAILED",
                "Failed to delete task and dismiss reminder. Please try again."));
        }
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
