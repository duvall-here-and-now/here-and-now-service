using HereAndNowService.Models;
using HereAndNowService.Models.Exceptions;
using HereAndNowService.Repositories;
using Microsoft.Extensions.Logging;

namespace HereAndNowService.Services;

/// <summary>
/// Service implementation for Task business logic
/// </summary>
public class TaskService : ITaskService
{
    private readonly ITaskRepository _taskRepository;
    private readonly ITaskReminderRepository _reminderRepository;
    private readonly ILogger<TaskService> _logger;

    /// <summary>
    /// Creates a new TaskService instance
    /// </summary>
    public TaskService(
        ITaskRepository taskRepository,
        ITaskReminderRepository reminderRepository,
        ILogger<TaskService> logger)
    {
        _taskRepository = taskRepository;
        _reminderRepository = reminderRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TaskDocument> CreateTaskAsync(string name, string userId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Task name cannot be empty", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
        }

        _logger.LogDebug("Creating task '{Name}' for user {UserId}", name, userId);

        var now = DateTime.UtcNow;
        var task = new TaskDocument
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            Name = name.Trim(),
            State = TaskState.OnDeck,
            CreatedAt = now,
            CompletedAt = null,
            ReminderId = null,
            LastModifiedAt = now
        };

        var createdTask = await _taskRepository.CreateAsync(task);

        _logger.LogInformation("Created task {TaskId} '{Name}' for user {UserId}",
            createdTask.Id, createdTask.Name, userId);

        return createdTask;
    }

    /// <inheritdoc />
    public async Task<TaskDocument> CreateTaskWithIdAsync(string userId, string taskId, string name)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(taskId))
        {
            throw new ArgumentException("Task ID cannot be empty", nameof(taskId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Task name cannot be empty", nameof(name));
        }

        _logger.LogDebug("Creating task with client ID '{TaskId}' for user {UserId}", taskId, userId);

        // Check if task already exists
        var exists = await _taskRepository.ExistsAsync(userId, taskId);
        if (exists)
        {
            _logger.LogWarning("Task {TaskId} already exists for user {UserId}", taskId, userId);
            throw new TaskAlreadyExistsException(taskId);
        }

        var now = DateTime.UtcNow;
        var task = new TaskDocument
        {
            Id = taskId,
            UserId = userId,
            Name = name.Trim(),
            State = TaskState.OnDeck,
            CreatedAt = now,
            CompletedAt = null,
            ReminderId = null,
            LastModifiedAt = now
        };

        var createdTask = await _taskRepository.CreateAsync(task);

        _logger.LogInformation("Created task with client ID {TaskId} '{Name}' for user {UserId}",
            createdTask.Id, createdTask.Name, userId);

        return createdTask;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TaskDocument>> GetTasksAsync(string userId, string? state = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
        }

        // Validate state if provided
        if (state is not null && !TaskState.IsValid(state))
        {
            throw new ArgumentException($"Invalid task state: {state}", nameof(state));
        }

        _logger.LogDebug("Getting tasks for user {UserId} with state filter {State}",
            userId, state ?? "all");

        return await _taskRepository.GetByUserIdAsync(userId, state);
    }

    /// <inheritdoc />
    public async Task<PagedResult<TaskDocument>> GetTasksPagedAsync(
        string userId,
        string? state = null,
        string orderBy = "createdAt",
        string direction = "asc",
        int skip = 0,
        int take = 50)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
        }

        // Validate state if provided
        if (state is not null && !TaskState.IsValid(state))
        {
            throw new ArgumentException($"Invalid task state: {state}", nameof(state));
        }

        // Validate orderBy
        if (!orderBy.Equals("createdAt", StringComparison.OrdinalIgnoreCase) &&
            !orderBy.Equals("completedAt", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Invalid orderBy value: {orderBy}. Must be 'createdAt' or 'completedAt'", nameof(orderBy));
        }

        // Validate direction
        if (!direction.Equals("asc", StringComparison.OrdinalIgnoreCase) &&
            !direction.Equals("desc", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Invalid direction value: {direction}. Must be 'asc' or 'desc'", nameof(direction));
        }

        // Validate skip
        if (skip < 0)
        {
            throw new ArgumentException("Skip cannot be negative", nameof(skip));
        }

        // Validate take
        if (take < 1 || take > 100)
        {
            throw new ArgumentException("Take must be between 1 and 100", nameof(take));
        }

        _logger.LogDebug(
            "Getting paged tasks for user {UserId}: state={State}, orderBy={OrderBy}, direction={Direction}, skip={Skip}, take={Take}",
            userId, state ?? "all", orderBy, direction, skip, take);

        return await _taskRepository.GetByUserIdPagedAsync(userId, state, orderBy, direction, skip, take);
    }

    /// <inheritdoc />
    public async Task<TaskDocument> GetTaskByIdAsync(string taskId, string userId)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            throw new ArgumentException("Task ID cannot be empty", nameof(taskId));
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
        }

        _logger.LogDebug("Getting task {TaskId} for user {UserId}", taskId, userId);

        var task = await _taskRepository.GetByIdAsync(userId, taskId);

        if (task is null)
        {
            throw new TaskNotFoundException(taskId);
        }

        return task;
    }

    /// <inheritdoc />
    public async Task<TaskDocument> UpdateTaskAsync(string taskId, string userId, string? name, string? state)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            throw new ArgumentException("Task ID cannot be empty", nameof(taskId));
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
        }

        _logger.LogDebug("Updating task {TaskId} for user {UserId}", taskId, userId);

        // Fetch the existing task
        var task = await _taskRepository.GetByIdAsync(userId, taskId);

        if (task is null)
        {
            throw new TaskNotFoundException(taskId);
        }

        // Check if task is in Deleted state - deleted tasks cannot be modified
        if (task.State == TaskState.Deleted)
        {
            throw new InvalidStateTransitionException(
                taskId,
                TaskState.Deleted,
                "UpdateTask",
                "Deleted tasks cannot be modified");
        }

        // Capture timestamp once for consistency across all field updates
        var now = DateTime.UtcNow;

        // Update name if provided
        if (!string.IsNullOrWhiteSpace(name))
        {
            task.Name = name.Trim();
        }

        // Handle state transitions with completedAt logic
        if (!string.IsNullOrWhiteSpace(state))
        {
            // Validate state at service layer (defense in depth)
            if (!TaskState.IsValid(state))
            {
                throw new ArgumentException($"Invalid task state: {state}", nameof(state));
            }

            var isTransitioningToCompleted = state == TaskState.Completed && task.State != TaskState.Completed;
            var isTransitioningFromCompleted = state != TaskState.Completed && task.State == TaskState.Completed;

            task.State = state;

            if (isTransitioningToCompleted)
            {
                task.CompletedAt = now;
                _logger.LogDebug("Task {TaskId} marked completed, setting completedAt", taskId);
            }
            else if (isTransitioningFromCompleted)
            {
                task.CompletedAt = null;
                _logger.LogDebug("Task {TaskId} transitioned from Completed, clearing completedAt", taskId);
            }
            // Otherwise: preserve existing completedAt
        }

        // Always update LastModifiedAt on any change
        task.LastModifiedAt = now;

        // If name changed and task has a reminder, sync the denormalized TaskName atomically
        if (!string.IsNullOrWhiteSpace(name))
        {
            var reminder = await GetReminderForSyncAsync(task, userId);
            if (reminder != null)
            {
                reminder.TaskName = task.Name;
                reminder.LastModifiedAt = now;

                var updatedTask = await _taskRepository.UpdateWithReminderSyncAsync(task, reminder);

                _logger.LogInformation("Updated task {TaskId} with reminder sync for user {UserId}",
                    taskId, userId);

                return updatedTask;
            }
        }

        var result = await _taskRepository.UpdateAsync(task);

        _logger.LogInformation("Updated task {TaskId} for user {UserId}", taskId, userId);

        return result;
    }

    /// <inheritdoc />
    public async Task<TaskDocument> CreateTaskWithOptionalReminderAsync(string name, string userId, DateTime? scheduledTime)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Task name cannot be empty", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
        }

        _logger.LogDebug("Creating task '{Name}' for user {UserId} with reminder: {HasReminder}",
            name, userId, scheduledTime.HasValue);

        // 1. Create the Task first
        var now = DateTime.UtcNow;
        var task = new TaskDocument
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            Name = name.Trim(),
            State = TaskState.OnDeck,
            CreatedAt = now,
            CompletedAt = null,
            ReminderId = null,
            LastModifiedAt = now
        };

        var createdTask = await _taskRepository.CreateAsync(task);

        _logger.LogInformation("Created task {TaskId} '{Name}' for user {UserId}",
            createdTask.Id, createdTask.Name, userId);

        // 2. If scheduledTime provided, create reminder and link to task
        if (scheduledTime.HasValue)
        {
            _logger.LogDebug("Creating reminder for task {TaskId} scheduled at {ScheduledTime}",
                createdTask.Id, scheduledTime.Value);

            var reminder = new TaskReminderDocument
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                TaskId = createdTask.Id,
                TaskName = createdTask.Name,
                // Use ToUniversalTime() for proper conversion (not SpecifyKind which only relabels)
                ScheduledTime = scheduledTime.Value.Kind == DateTimeKind.Utc
                    ? scheduledTime.Value
                    : scheduledTime.Value.ToUniversalTime(),
                IsDismissed = false,
                CreatedAt = now,
                LastModifiedAt = now
            };

            // Use atomic transactional method to ensure both reminder creation and task linking succeed or fail together
            var createdReminder = await _reminderRepository.CreateWithTaskLinkAsync(reminder, createdTask.Id);

            // Update local object to reflect the change made by the transaction
            createdTask.ReminderId = createdReminder.Id;

            _logger.LogInformation("Created task {TaskId} with reminder {ReminderId} for user {UserId}",
                createdTask.Id, createdReminder.Id, userId);
        }

        return createdTask;
    }

    /// <inheritdoc />
    public async Task<TaskDocument> CompleteTaskWithUnityAsync(string userId, string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            throw new ArgumentException("Task ID cannot be empty", nameof(taskId));
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
        }

        _logger.LogDebug("Completing task {TaskId} with Unity for user {UserId}", taskId, userId);

        // 1. Load the task
        var task = await _taskRepository.GetByIdAsync(userId, taskId);
        if (task == null)
        {
            throw new TaskNotFoundException(taskId);
        }

        // 2. Check if task has a reminder
        TaskReminderDocument? reminder = null;
        if (!string.IsNullOrEmpty(task.ReminderId))
        {
            reminder = await _reminderRepository.GetByIdAsync(userId, task.ReminderId);
            if (reminder == null)
            {
                // Reminder was deleted - clear the stale reference
                _logger.LogWarning(
                    "Task {TaskId} has reminderId {ReminderId} but reminder not found - clearing stale reference",
                    taskId, task.ReminderId);
                task.ReminderId = null;
            }
        }

        // 3. Prepare updates
        var now = DateTime.UtcNow;
        task.State = TaskState.Completed;
        task.CompletedAt = now;
        task.LastModifiedAt = now;

        if (reminder != null)
        {
            reminder.IsDismissed = true;
            reminder.DismissedAt = now;
            reminder.LastModifiedAt = now;
            task.ReminderId = null;
        }

        // 4. Execute atomic Unity operation (batch for task+reminder, or simple update if no reminder)
        var completedTask = await _taskRepository.CompleteWithUnityAsync(task, reminder);

        _logger.LogInformation(
            "Completed task {TaskId} with Unity for user {UserId}, reminderDismissed={ReminderDismissed}",
            taskId, userId, reminder != null);

        return completedTask;
    }

    /// <inheritdoc />
    public async Task DeleteTaskWithUnityAsync(string userId, string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            throw new ArgumentException("Task ID cannot be empty", nameof(taskId));
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
        }

        _logger.LogDebug("Deleting task {TaskId} with Unity for user {UserId}", taskId, userId);

        // 1. Load the task
        var task = await _taskRepository.GetByIdAsync(userId, taskId);
        if (task == null)
        {
            throw new TaskNotFoundException(taskId);
        }

        // 2. Check if task has a reminder
        TaskReminderDocument? reminder = null;
        if (!string.IsNullOrEmpty(task.ReminderId))
        {
            reminder = await _reminderRepository.GetByIdAsync(userId, task.ReminderId);
            if (reminder == null)
            {
                // Reminder was already deleted - clear the stale reference
                _logger.LogWarning(
                    "Task {TaskId} has reminderId {ReminderId} but reminder not found - clearing stale reference",
                    taskId, task.ReminderId);
                task.ReminderId = null;
            }
        }

        // 3. Prepare updates - soft-delete the task
        var now = DateTime.UtcNow;
        task.State = TaskState.Deleted;
        task.LastModifiedAt = now;

        if (reminder != null)
        {
            reminder.IsDismissed = true;
            reminder.DismissedAt = now;
            reminder.LastModifiedAt = now;
        }

        // 4. Execute atomic Unity operation (batch for task+reminder, or simple update if no reminder)
        await _taskRepository.DeleteWithUnityAsync(task, reminder);

        _logger.LogInformation(
            "Deleted task {TaskId} with Unity for user {UserId}, reminderDismissed={ReminderDismissed}",
            taskId, userId, reminder != null);
    }

    /// <inheritdoc />
    public async Task<(TaskDocument Task, TaskReminderDocument Reminder)> CreateTaskWithReminderAsync(
        string userId,
        string taskId,
        string taskReminderId,
        string name,
        DateTime scheduledTime)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(taskId))
        {
            throw new ArgumentException("Task ID cannot be empty", nameof(taskId));
        }

        if (string.IsNullOrWhiteSpace(taskReminderId))
        {
            throw new ArgumentException("Reminder ID cannot be empty", nameof(taskReminderId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Task name cannot be empty", nameof(name));
        }

        _logger.LogDebug(
            "Creating task {TaskId} with reminder {ReminderId} for user {UserId}",
            taskId, taskReminderId, userId);

        // Pre-checks provide clearer error messages in the common case.
        // Note: A race condition exists between these checks and the batch execution -
        // concurrent requests could create documents between check and batch.
        // The batch operation handles this with proper 409 Conflict detection,
        // so correctness is guaranteed. Pre-checks are for better UX only.
        var taskExists = await _taskRepository.ExistsAsync(userId, taskId);
        if (taskExists)
        {
            _logger.LogWarning("Task {TaskId} already exists for user {UserId}", taskId, userId);
            throw new TaskAlreadyExistsException(taskId);
        }

        // Pre-check for existing reminder (provides better error message than batch failure)
        var reminderExists = await _reminderRepository.ExistsAsync(userId, taskReminderId);
        if (reminderExists)
        {
            _logger.LogWarning("Reminder {ReminderId} already exists for user {UserId}", taskReminderId, userId);
            throw new TaskReminderAlreadyExistsException(taskReminderId);
        }

        var now = DateTime.UtcNow;

        // Create Task document with reminderId already set for bidirectional link
        var task = new TaskDocument
        {
            Id = taskId,
            UserId = userId,
            Name = name.Trim(),
            State = TaskState.OnDeck,
            CreatedAt = now,
            CompletedAt = null,
            ReminderId = taskReminderId,
            LastModifiedAt = now
        };

        // Create TaskReminder document with taskId for bidirectional link
        var reminder = new TaskReminderDocument
        {
            Id = taskReminderId,
            UserId = userId,
            TaskId = taskId,
            TaskName = name.Trim(),
            // Ensure scheduledTime is stored as UTC
            ScheduledTime = scheduledTime.Kind == DateTimeKind.Utc
                ? scheduledTime
                : scheduledTime.ToUniversalTime(),
            IsDismissed = false,
            CreatedAt = now,
            LastModifiedAt = now
        };

        // Execute atomic batch creation
        var (createdTask, createdReminder) = await _taskRepository.CreateTaskWithReminderBatchAsync(task, reminder);

        _logger.LogInformation(
            "Created task {TaskId} with reminder {ReminderId} for user {UserId}",
            createdTask.Id, createdReminder.Id, userId);

        return (createdTask, createdReminder);
    }

    /// <inheritdoc />
    public async Task<TaskDocument> UpdateTaskNameAsync(string userId, string taskId, string name)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(taskId))
        {
            throw new ArgumentException("Task ID cannot be empty", nameof(taskId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Task name cannot be empty", nameof(name));
        }

        _logger.LogDebug("Updating task name for {TaskId} for user {UserId}", taskId, userId);

        // Fetch the existing task
        var task = await _taskRepository.GetByIdAsync(userId, taskId);

        if (task == null)
        {
            throw new TaskNotFoundException(taskId);
        }

        // Check if task is in Deleted state - deleted tasks cannot be modified
        if (task.State == TaskState.Deleted)
        {
            throw new InvalidStateTransitionException(
                taskId,
                TaskState.Deleted,
                "UpdateTaskName",
                "Deleted tasks cannot be modified");
        }

        var now = DateTime.UtcNow;
        task.Name = name.Trim();
        task.LastModifiedAt = now;

        // Check if task has an active reminder that needs sync
        var reminder = await GetReminderForSyncAsync(task, userId);
        if (reminder != null)
        {
            reminder.TaskName = task.Name;
            reminder.LastModifiedAt = now;

            var updatedTask = await _taskRepository.UpdateWithReminderSyncAsync(task, reminder);

            _logger.LogInformation("Updated task name for {TaskId} with reminder sync for user {UserId}",
                taskId, userId);

            return updatedTask;
        }

        // No active reminder - simple update
        var result = await _taskRepository.UpdateAsync(task);

        _logger.LogInformation("Updated task name for {TaskId} for user {UserId}", taskId, userId);

        return result;
    }

    /// <summary>
    /// Gets the reminder for a task if it needs to be synced with updated task name.
    /// Handles stale references by clearing task.ReminderId if reminder is not found.
    /// Returns null if no sync is needed (no reminder, stale reference, or dismissed).
    /// </summary>
    /// <param name="task">The task document (may be modified to clear stale ReminderId)</param>
    /// <param name="userId">The user ID for partition key</param>
    /// <returns>The reminder document if sync is needed, null otherwise</returns>
    private async Task<TaskReminderDocument?> GetReminderForSyncAsync(TaskDocument task, string userId)
    {
        if (string.IsNullOrEmpty(task.ReminderId))
        {
            return null;
        }

        var reminder = await _reminderRepository.GetByIdAsync(userId, task.ReminderId);

        if (reminder == null)
        {
            // Handle stale reference - reminder was deleted but task still references it
            _logger.LogWarning(
                "Task {TaskId} has reminderId {ReminderId} but reminder not found - clearing stale reference",
                task.Id, task.ReminderId);
            task.ReminderId = null;
            return null;
        }

        if (reminder.IsDismissed)
        {
            // Reminder exists but is dismissed - no sync needed
            _logger.LogDebug(
                "Skipping reminder sync for task {TaskId} - reminder {ReminderId} is already dismissed",
                task.Id, reminder.Id);
            return null;
        }

        // Active reminder exists - sync needed
        _logger.LogDebug("Syncing TaskName to reminder {ReminderId} for task {TaskId}",
            task.ReminderId, task.Id);
        return reminder;
    }
}
