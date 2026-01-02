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
    /// Creates a new task
    /// </summary>
    /// <param name="createTaskDto">The task creation request</param>
    /// <returns>The created task</returns>
    /// <response code="201">Returns the newly created task</response>
    /// <response code="400">If the request is invalid</response>
    /// <response code="401">If the user is not authenticated</response>
    [HttpPost]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TaskDto>> CreateTask([FromBody] CreateTaskDto createTaskDto)
    {
        var userId = GetUserId();

        _logger.LogInformation("Creating task for user {UserId}", userId);

        try
        {
            var task = await _taskService.CreateTaskAsync(createTaskDto.Name, userId);
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
    /// Gets all tasks for the authenticated user
    /// </summary>
    /// <param name="state">Optional filter by task state (OnDeck, InProgress, Completed)</param>
    /// <returns>List of tasks</returns>
    /// <response code="200">Returns the list of tasks</response>
    /// <response code="400">If the state filter is invalid</response>
    /// <response code="401">If the user is not authenticated</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TaskDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<TaskDto>>> GetTasks([FromQuery] string? state = null)
    {
        var userId = GetUserId();

        _logger.LogInformation("Getting tasks for user {UserId} with state filter {State}",
            userId, state ?? "all");

        try
        {
            var tasks = await _taskService.GetTasksAsync(userId, state);
            return Ok(TaskMapper.ToDtoList(tasks));
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
