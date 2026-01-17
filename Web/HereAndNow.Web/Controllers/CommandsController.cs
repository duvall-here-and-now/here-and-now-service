using System.Security.Claims;
using System.Text.Json;
using HereAndNowService.Commands;
using HereAndNowService.DTOs;
using HereAndNowService.Mappers;
using HereAndNowService.Models.Exceptions;
using HereAndNowService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HereAndNowService.Controllers;

/// <summary>
/// Controller for executing commands. Commands provide explicit intent-based operations
/// with client-generated IDs for optimistic UI patterns.
/// </summary>
/// <remarks>
/// Available commands:
/// - CreateTask: Create a new task with a client-generated ID
///
/// Future commands (Stories 6.2-6.5):
/// - CreateTaskAndTaskReminder: Atomic task + reminder creation
/// - UpdateTaskName: Change task name with reminder sync
/// - UpdateTaskState: All state transitions with Unity
/// - UpdateTaskReminderScheduledTime: Reschedule reminder
/// - DismissTaskReminder: Dismiss reminder only
/// </remarks>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class CommandsController : ControllerBase
{
    private readonly ITaskService _taskService;
    private readonly ILogger<CommandsController> _logger;

    /// <summary>
    /// Creates a new CommandsController instance
    /// </summary>
    public CommandsController(ITaskService taskService, ILogger<CommandsController> logger)
    {
        _taskService = taskService;
        _logger = logger;
    }

    /// <summary>
    /// Executes a command to modify system state.
    /// </summary>
    /// <remarks>
    /// Available commands: CreateTask
    ///
    /// Request format:
    /// ```json
    /// {
    ///   "command": "CreateTask",
    ///   "payload": {
    ///     "taskId": "550e8400-e29b-41d4-a716-446655440000",
    ///     "name": "My New Task"
    ///   }
    /// }
    /// ```
    ///
    /// See API documentation for full payload structures.
    /// </remarks>
    /// <param name="request">The command request with type and payload</param>
    /// <returns>Command-specific response or error</returns>
    [HttpPost]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ExecuteCommand([FromBody] CommandRequest request)
    {
        var userId = GetUserId();

        _logger.LogInformation("Executing command {Command} for user {UserId}", request.Command, userId);

        return request.Command switch
        {
            "CreateTask" => await HandleCreateTaskAsync(request, userId),
            _ => BadRequest(CreateErrorResponse("UNKNOWN_COMMAND", $"Unknown command: {request.Command}"))
        };
    }

    /// <summary>
    /// Handles the CreateTask command
    /// </summary>
    private async Task<IActionResult> HandleCreateTaskAsync(CommandRequest request, string userId)
    {
        // Deserialize payload to CreateTaskCommand
        CreateTaskCommand? command;
        try
        {
            command = JsonSerializer.Deserialize<CreateTaskCommand>(
                request.Payload.GetRawText(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize CreateTask payload");
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "Invalid payload format for CreateTask command"));
        }

        if (command == null)
        {
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "Payload is required for CreateTask command"));
        }

        // Validate taskId is provided
        if (string.IsNullOrWhiteSpace(command.TaskId))
        {
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "taskId is required"));
        }

        // Validate taskId is a valid GUID format
        if (!Guid.TryParse(command.TaskId, out var parsedGuid))
        {
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "taskId must be a valid GUID format"));
        }

        // Use consistent lowercase GUID format
        var taskId = parsedGuid.ToString();

        // Validate name is provided and not empty
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "name is required and cannot be empty"));
        }

        _logger.LogDebug("Creating task with client-generated ID {TaskId} for user {UserId}", taskId, userId);

        try
        {
            var task = await _taskService.CreateTaskWithIdAsync(userId, taskId, command.Name);
            var taskDto = TaskMapper.ToDto(task);

            return CreatedAtAction(
                actionName: "GetTask",
                controllerName: "Tasks",
                routeValues: new { id = taskDto.Id },
                value: taskDto);
        }
        catch (TaskAlreadyExistsException)
        {
            return Conflict(CreateErrorResponse("TASK_ALREADY_EXISTS", $"Task with ID {taskId} already exists"));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid CreateTask request");
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", ex.Message));
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
