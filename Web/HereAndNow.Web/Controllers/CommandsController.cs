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
/// - CreateTaskAndTaskReminder: Atomic task + reminder creation with client-generated IDs
/// - UpdateTaskName: Change task name with automatic reminder denormalization sync
/// - UpdateTaskState: Change task state with automatic reminder dismissal (Unity) for Completed/Deleted
/// - UpdateTaskReminderScheduledTime: Reschedule/snooze a reminder to a new time
/// - DismissTaskReminder: Dismiss a reminder (idempotent operation)
/// - CreateRecurringTaskConfig: Create a new recurring task configuration
/// - UpdateRecurringTaskConfig: Update an existing recurring task configuration
/// - DeleteRecurringTaskConfig: Delete a recurring task configuration and its state overrides
/// </remarks>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class CommandsController : ControllerBase
{
    private readonly ITaskService _taskService;
    private readonly ITaskReminderService _reminderService;
    private readonly IRecurringTaskService _recurringTaskService;
    private readonly ILogger<CommandsController> _logger;

    /// <summary>
    /// Creates a new CommandsController instance
    /// </summary>
    /// <param name="taskService">The task service for task-related operations</param>
    /// <param name="reminderService">The reminder service for reminder-related operations</param>
    /// <param name="recurringTaskService">The recurring task service for recurring task config operations</param>
    /// <param name="logger">The logger instance</param>
    public CommandsController(
        ITaskService taskService,
        ITaskReminderService reminderService,
        IRecurringTaskService recurringTaskService,
        ILogger<CommandsController> logger)
    {
        _taskService = taskService;
        _reminderService = reminderService;
        _recurringTaskService = recurringTaskService;
        _logger = logger;
    }

    /// <summary>
    /// Executes a command to modify system state.
    /// </summary>
    /// <remarks>
    /// Available commands: CreateTask, CreateTaskAndTaskReminder, UpdateTaskName, UpdateTaskState, UpdateTaskReminderScheduledTime, DismissTaskReminder
    ///
    /// Request format for CreateTask:
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
    /// Request format for CreateTaskAndTaskReminder:
    /// ```json
    /// {
    ///   "command": "CreateTaskAndTaskReminder",
    ///   "payload": {
    ///     "taskId": "550e8400-e29b-41d4-a716-446655440000",
    ///     "taskReminderId": "660e8400-e29b-41d4-a716-446655440001",
    ///     "name": "Call dentist",
    ///     "scheduledTime": "2026-01-20T09:00:00Z"
    ///   }
    /// }
    /// ```
    ///
    /// Request format for UpdateTaskState:
    /// ```json
    /// {
    ///   "command": "UpdateTaskState",
    ///   "payload": {
    ///     "taskId": "550e8400-e29b-41d4-a716-446655440000",
    ///     "state": "Completed"
    ///   }
    /// }
    /// ```
    /// Valid states: OnDeck, InProgress, Completed, Deleted (case-sensitive)
    /// Notes: Idempotent (same state = no-op), Deleted is terminal, Unity dismisses reminder on Completed/Deleted
    ///
    /// Request format for UpdateTaskReminderScheduledTime:
    /// ```json
    /// {
    ///   "command": "UpdateTaskReminderScheduledTime",
    ///   "payload": {
    ///     "taskReminderId": "660e8400-e29b-41d4-a716-446655440001",
    ///     "scheduledTime": "2026-01-25T14:00:00Z"
    ///   }
    /// }
    /// ```
    ///
    /// Request format for DismissTaskReminder:
    /// ```json
    /// {
    ///   "command": "DismissTaskReminder",
    ///   "payload": {
    ///     "taskReminderId": "660e8400-e29b-41d4-a716-446655440001"
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
    [ProducesResponseType(typeof(TaskReminderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ExecuteCommand([FromBody] CommandRequest request)
    {
        var userId = GetUserId();

        _logger.LogInformation("Executing command {Command} for user {UserId}", request.Command, userId);

        return request.Command switch
        {
            "CreateTask" => await HandleCreateTaskAsync(request, userId),
            "CreateTaskAndTaskReminder" => await HandleCreateTaskAndTaskReminderAsync(request, userId),
            "UpdateTaskName" => await HandleUpdateTaskNameAsync(request, userId),
            "UpdateTaskState" => await HandleUpdateTaskStateAsync(request, userId),
            "UpdateTaskReminderScheduledTime" => await HandleUpdateTaskReminderScheduledTimeAsync(request, userId),
            "DismissTaskReminder" => await HandleDismissTaskReminderAsync(request, userId),
            "CreateRecurringTaskConfig" => await HandleCreateRecurringTaskConfigAsync(request, userId),
            "UpdateRecurringTaskConfig" => await HandleUpdateRecurringTaskConfigAsync(request, userId),
            "DeleteRecurringTaskConfig" => await HandleDeleteRecurringTaskConfigAsync(request, userId),
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
        var taskId = parsedGuid.ToString().ToLowerInvariant();

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
    /// Handles the CreateTaskAndTaskReminder command
    /// </summary>
    private async Task<IActionResult> HandleCreateTaskAndTaskReminderAsync(CommandRequest request, string userId)
    {
        // Deserialize payload to CreateTaskAndTaskReminderCommand
        CreateTaskAndTaskReminderCommand? command;
        try
        {
            command = JsonSerializer.Deserialize<CreateTaskAndTaskReminderCommand>(
                request.Payload.GetRawText(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize CreateTaskAndTaskReminder payload");
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "Invalid payload format for CreateTaskAndTaskReminder command"));
        }

        if (command == null)
        {
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "Payload is required for CreateTaskAndTaskReminder command"));
        }

        // Validate taskId is provided
        if (string.IsNullOrWhiteSpace(command.TaskId))
        {
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "taskId is required"));
        }

        // Validate taskId is a valid GUID format
        if (!Guid.TryParse(command.TaskId, out var parsedTaskGuid))
        {
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "taskId must be a valid GUID format"));
        }

        // Validate taskReminderId is provided
        if (string.IsNullOrWhiteSpace(command.TaskReminderId))
        {
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "taskReminderId is required"));
        }

        // Validate taskReminderId is a valid GUID format
        if (!Guid.TryParse(command.TaskReminderId, out var parsedReminderGuid))
        {
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "taskReminderId must be a valid GUID format"));
        }

        // Use consistent lowercase GUID format
        var taskId = parsedTaskGuid.ToString().ToLowerInvariant();
        var taskReminderId = parsedReminderGuid.ToString().ToLowerInvariant();

        // Validate name is provided and not empty
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "name is required and cannot be empty"));
        }

        // Note: We intentionally do NOT validate scheduledTime is in the future here.
        // This command may arrive via delayed sync from a mobile client, where the creation
        // was valid when performed but the scheduled time has since passed.
        // The mobile client validates future time at the moment of user interaction.

        _logger.LogDebug(
            "Creating task with client-generated ID {TaskId} and reminder {ReminderId} for user {UserId}",
            taskId, taskReminderId, userId);

        try
        {
            var (task, reminder) = await _taskService.CreateTaskWithReminderAsync(
                userId,
                taskId,
                taskReminderId,
                command.Name,
                command.ScheduledTime);

            var responseDto = new TaskAndReminderDto
            {
                Task = TaskMapper.ToDto(task),
                Reminder = ReminderMapper.ToDto(reminder)
            };

            return CreatedAtAction(
                actionName: "GetTask",
                controllerName: "Tasks",
                routeValues: new { id = responseDto.Task.Id },
                value: responseDto);
        }
        catch (TaskAlreadyExistsException)
        {
            return Conflict(CreateErrorResponse("TASK_ALREADY_EXISTS", $"Task with ID {taskId} already exists"));
        }
        catch (TaskReminderAlreadyExistsException)
        {
            return Conflict(CreateErrorResponse("TASK_REMINDER_ALREADY_EXISTS", $"TaskReminder with ID {taskReminderId} already exists"));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid CreateTaskAndTaskReminder request");
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", ex.Message));
        }
    }

    /// <summary>
    /// Handles the UpdateTaskName command
    /// </summary>
    private async Task<IActionResult> HandleUpdateTaskNameAsync(CommandRequest request, string userId)
    {
        // Deserialize payload to UpdateTaskNameCommand
        UpdateTaskNameCommand? command;
        try
        {
            command = JsonSerializer.Deserialize<UpdateTaskNameCommand>(
                request.Payload.GetRawText(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize UpdateTaskName payload");
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "Invalid payload format for UpdateTaskName command"));
        }

        if (command == null)
        {
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "Payload is required for UpdateTaskName command"));
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
        var taskId = parsedGuid.ToString().ToLowerInvariant();

        // Validate name is provided and not empty/whitespace
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "name is required and cannot be empty"));
        }

        _logger.LogDebug("Updating task name for task {TaskId} for user {UserId}", taskId, userId);

        try
        {
            var task = await _taskService.UpdateTaskNameAsync(userId, taskId, command.Name);
            var taskDto = TaskMapper.ToDto(task);

            return Ok(taskDto);
        }
        catch (TaskNotFoundException)
        {
            return NotFound(CreateErrorResponse("TASK_NOT_FOUND", $"Task with ID {taskId} not found"));
        }
        catch (InvalidStateTransitionException ex)
        {
            return BadRequest(CreateErrorResponse("INVALID_STATE_TRANSITION", ex.Message));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid UpdateTaskName request");
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", ex.Message));
        }
    }

    /// <summary>
    /// Handles the UpdateTaskState command
    /// </summary>
    private async Task<IActionResult> HandleUpdateTaskStateAsync(CommandRequest request, string userId)
    {
        // Deserialize payload to UpdateTaskStateCommand
        UpdateTaskStateCommand? command;
        try
        {
            command = JsonSerializer.Deserialize<UpdateTaskStateCommand>(
                request.Payload.GetRawText(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize UpdateTaskState payload");
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "Invalid payload format for UpdateTaskState command"));
        }

        if (command == null)
        {
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "Payload is required for UpdateTaskState command"));
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
        var taskId = parsedGuid.ToString().ToLowerInvariant();

        // Validate state is provided
        if (string.IsNullOrWhiteSpace(command.State))
        {
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "state is required"));
        }

        // Validate state is one of the valid values (case-sensitive)
        var validStates = new[] { "OnDeck", "InProgress", "Completed", "Deleted" };
        if (!validStates.Contains(command.State))
        {
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "State must be one of: OnDeck, InProgress, Completed, Deleted"));
        }

        _logger.LogDebug("Updating task {TaskId} state to {State} for user {UserId}", taskId, command.State, userId);

        try
        {
            var task = await _taskService.UpdateStateAsync(userId, taskId, command.State);
            var taskDto = TaskMapper.ToDto(task);

            return Ok(taskDto);
        }
        catch (TaskNotFoundException)
        {
            return NotFound(CreateErrorResponse("TASK_NOT_FOUND", $"Task with ID {taskId} not found"));
        }
        catch (InvalidStateTransitionException ex)
        {
            return BadRequest(CreateErrorResponse("INVALID_STATE_TRANSITION", ex.Message));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid UpdateTaskState request");
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", ex.Message));
        }
    }

    /// <summary>
    /// Handles the UpdateTaskReminderScheduledTime command
    /// </summary>
    private async Task<IActionResult> HandleUpdateTaskReminderScheduledTimeAsync(CommandRequest request, string userId)
    {
        // Deserialize payload to UpdateTaskReminderScheduledTimeCommand
        UpdateTaskReminderScheduledTimeCommand? command;
        try
        {
            command = JsonSerializer.Deserialize<UpdateTaskReminderScheduledTimeCommand>(
                request.Payload.GetRawText(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize UpdateTaskReminderScheduledTime payload");
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "Invalid payload format for UpdateTaskReminderScheduledTime command"));
        }

        if (command == null)
        {
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "Payload is required for UpdateTaskReminderScheduledTime command"));
        }

        // Validate taskReminderId is provided
        if (string.IsNullOrWhiteSpace(command.TaskReminderId))
        {
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "taskReminderId is required"));
        }

        // Validate taskReminderId is a valid GUID format
        if (!Guid.TryParse(command.TaskReminderId, out var parsedGuid))
        {
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "taskReminderId must be a valid GUID format"));
        }

        // Use consistent lowercase GUID format
        var taskReminderId = parsedGuid.ToString().ToLowerInvariant();

        // Note: We intentionally do NOT validate scheduledTime is in the future here.
        // This command may arrive via delayed sync from a mobile client, where the snooze
        // was valid when performed but the scheduled time has since passed.
        // The mobile client validates future time at the moment of user interaction.

        _logger.LogDebug("Rescheduling reminder {ReminderId} to {ScheduledTime} for user {UserId}",
            taskReminderId, command.ScheduledTime, userId);

        try
        {
            var reminder = await _reminderService.SnoozeAsync(userId, taskReminderId, command.ScheduledTime);
            var reminderDto = ReminderMapper.ToDto(reminder);

            return Ok(reminderDto);
        }
        catch (ReminderNotFoundException)
        {
            return NotFound(CreateErrorResponse("REMINDER_NOT_FOUND", $"Reminder with ID {taskReminderId} not found"));
        }
        catch (ReminderAlreadyDismissedException)
        {
            return BadRequest(CreateErrorResponse("REMINDER_ALREADY_DISMISSED", $"Reminder {taskReminderId} has already been dismissed"));
        }
        catch (InvalidScheduledTimeException ex)
        {
            return BadRequest(CreateErrorResponse("INVALID_SCHEDULED_TIME", ex.Message));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid UpdateTaskReminderScheduledTime request");
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", ex.Message));
        }
    }

    /// <summary>
    /// Handles the DismissTaskReminder command (idempotent operation)
    /// </summary>
    private async Task<IActionResult> HandleDismissTaskReminderAsync(CommandRequest request, string userId)
    {
        // Deserialize payload to DismissTaskReminderCommand
        DismissTaskReminderCommand? command;
        try
        {
            command = JsonSerializer.Deserialize<DismissTaskReminderCommand>(
                request.Payload.GetRawText(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize DismissTaskReminder payload");
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "Invalid payload format for DismissTaskReminder command"));
        }

        if (command == null)
        {
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "Payload is required for DismissTaskReminder command"));
        }

        // Validate taskReminderId is provided
        if (string.IsNullOrWhiteSpace(command.TaskReminderId))
        {
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "taskReminderId is required"));
        }

        // Validate taskReminderId is a valid GUID format
        if (!Guid.TryParse(command.TaskReminderId, out var parsedGuid))
        {
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "taskReminderId must be a valid GUID format"));
        }

        // Use consistent lowercase GUID format
        var taskReminderId = parsedGuid.ToString().ToLowerInvariant();

        _logger.LogDebug("Dismissing reminder {ReminderId} for user {UserId}", taskReminderId, userId);

        try
        {
            await _reminderService.DismissAsync(userId, taskReminderId);

            return NoContent();
        }
        catch (ReminderNotFoundException)
        {
            return NotFound(CreateErrorResponse("REMINDER_NOT_FOUND", $"Reminder with ID {taskReminderId} not found"));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid DismissTaskReminder request");
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", ex.Message));
        }
    }

    /// <summary>
    /// Handles the CreateRecurringTaskConfig command
    /// </summary>
    private async Task<IActionResult> HandleCreateRecurringTaskConfigAsync(CommandRequest request, string userId)
    {
        CreateRecurringTaskConfigCommand? command;
        try
        {
            command = JsonSerializer.Deserialize<CreateRecurringTaskConfigCommand>(
                request.Payload.GetRawText(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize CreateRecurringTaskConfig payload");
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR",
                "Invalid payload format for CreateRecurringTaskConfig command"));
        }

        if (command == null)
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR",
                "Payload is required for CreateRecurringTaskConfig command"));

        if (string.IsNullOrWhiteSpace(command.Id))
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "id is required"));
        if (!Guid.TryParse(command.Id, out var parsedGuid))
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "id must be a valid GUID format"));
        var configId = parsedGuid.ToString().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(command.Text))
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "text is required and cannot be empty"));

        if (string.IsNullOrWhiteSpace(command.RecurrenceRule))
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "recurrenceRule is required"));

        if (command.StartDateAndTime == default)
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "startDateAndTime is required"));

        if (command.StartDateAndTime.Kind != DateTimeKind.Utc)
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "startDateAndTime must be a UTC timestamp"));

        _logger.LogDebug("Creating recurring task config with ID {ConfigId} for user {UserId}", configId, userId);

        try
        {
            var config = await _recurringTaskService.CreateConfigAsync(
                userId, configId, command.Text, command.RecurrenceRule, command.StartDateAndTime);
            return StatusCode(StatusCodes.Status201Created, config);
        }
        catch (RecurringTaskConfigAlreadyExistsException)
        {
            return Conflict(CreateErrorResponse("RECURRING_TASK_CONFIG_ALREADY_EXISTS",
                $"Recurring task config with ID {configId} already exists"));
        }
        catch (InvalidRecurrenceRuleException ex)
        {
            return BadRequest(CreateErrorResponse("INVALID_RECURRENCE_RULE", ex.Message));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid CreateRecurringTaskConfig request");
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", ex.Message));
        }
    }

    /// <summary>
    /// Handles the UpdateRecurringTaskConfig command
    /// </summary>
    private async Task<IActionResult> HandleUpdateRecurringTaskConfigAsync(CommandRequest request, string userId)
    {
        UpdateRecurringTaskConfigCommand? command;
        try
        {
            command = JsonSerializer.Deserialize<UpdateRecurringTaskConfigCommand>(
                request.Payload.GetRawText(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize UpdateRecurringTaskConfig payload");
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR",
                "Invalid payload format for UpdateRecurringTaskConfig command"));
        }

        if (command == null)
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR",
                "Payload is required for UpdateRecurringTaskConfig command"));

        if (string.IsNullOrWhiteSpace(command.Id))
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "id is required"));
        if (!Guid.TryParse(command.Id, out var parsedGuid))
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "id must be a valid GUID format"));
        var configId = parsedGuid.ToString().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(command.Text))
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "text is required and cannot be empty"));

        if (string.IsNullOrWhiteSpace(command.RecurrenceRule))
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "recurrenceRule is required"));

        if (command.StartDateAndTime == default)
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "startDateAndTime is required"));

        if (command.StartDateAndTime.Kind != DateTimeKind.Utc)
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "startDateAndTime must be a UTC timestamp"));

        _logger.LogDebug("Updating recurring task config {ConfigId} for user {UserId}", configId, userId);

        try
        {
            var config = await _recurringTaskService.UpdateConfigAsync(
                userId, configId, command.Text, command.RecurrenceRule, command.StartDateAndTime);
            return Ok(config);
        }
        catch (RecurringTaskConfigNotFoundException)
        {
            return NotFound(CreateErrorResponse("RECURRING_TASK_CONFIG_NOT_FOUND",
                $"Recurring task config with ID {configId} not found"));
        }
        catch (InvalidRecurrenceRuleException ex)
        {
            return BadRequest(CreateErrorResponse("INVALID_RECURRENCE_RULE", ex.Message));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid UpdateRecurringTaskConfig request");
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", ex.Message));
        }
    }

    /// <summary>
    /// Handles the DeleteRecurringTaskConfig command
    /// </summary>
    private async Task<IActionResult> HandleDeleteRecurringTaskConfigAsync(CommandRequest request, string userId)
    {
        DeleteRecurringTaskConfigCommand? command;
        try
        {
            command = JsonSerializer.Deserialize<DeleteRecurringTaskConfigCommand>(
                request.Payload.GetRawText(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize DeleteRecurringTaskConfig payload");
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR",
                "Invalid payload format for DeleteRecurringTaskConfig command"));
        }

        if (command == null)
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR",
                "Payload is required for DeleteRecurringTaskConfig command"));

        if (string.IsNullOrWhiteSpace(command.Id))
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "id is required"));
        if (!Guid.TryParse(command.Id, out var parsedGuid))
            return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "id must be a valid GUID format"));
        var configId = parsedGuid.ToString().ToLowerInvariant();

        _logger.LogDebug("Deleting recurring task config {ConfigId} for user {UserId}", configId, userId);

        try
        {
            await _recurringTaskService.DeleteConfigAsync(userId, configId);
            return NoContent();
        }
        catch (RecurringTaskConfigNotFoundException)
        {
            return NotFound(CreateErrorResponse("RECURRING_TASK_CONFIG_NOT_FOUND",
                $"Recurring task config with ID {configId} not found"));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid DeleteRecurringTaskConfig request");
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
