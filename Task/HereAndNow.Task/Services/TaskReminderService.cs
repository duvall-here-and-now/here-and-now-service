using HereAndNowService.Models;
using HereAndNowService.Models.Exceptions;
using HereAndNowService.Repositories;
using Microsoft.Extensions.Logging;

namespace HereAndNowService.Services;

/// <summary>
/// Service implementation for TaskReminder business logic
/// </summary>
public class TaskReminderService : ITaskReminderService
{
    private readonly ITaskReminderRepository _reminderRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly ILogger<TaskReminderService> _logger;

    /// <summary>
    /// Creates a new TaskReminderService instance
    /// </summary>
    public TaskReminderService(
        ITaskReminderRepository reminderRepository,
        ITaskRepository taskRepository,
        ILogger<TaskReminderService> logger)
    {
        _reminderRepository = reminderRepository;
        _taskRepository = taskRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TaskReminderDocument> CreateReminderAsync(string userId, string taskId, DateTime scheduledTime)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(taskId))
        {
            throw new ArgumentException("Task ID cannot be empty", nameof(taskId));
        }

        _logger.LogDebug("Creating reminder for task {TaskId} by user {UserId}", taskId, userId);

        // 1. Load and validate task exists
        var task = await _taskRepository.GetByIdAsync(userId, taskId);
        if (task is null)
        {
            throw new TaskNotFoundException(taskId);
        }

        // 2. Check task doesn't already have a reminder
        var existingReminder = await _reminderRepository.GetByTaskIdAsync(userId, taskId);
        if (existingReminder is not null)
        {
            throw new ReminderAlreadyExistsException(taskId);
        }

        // 3. Create reminder with denormalized taskName
        var now = DateTime.UtcNow;
        var reminder = new TaskReminderDocument
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            TaskId = taskId,
            TaskName = task.Name,
            ScheduledTime = DateTime.SpecifyKind(scheduledTime, DateTimeKind.Utc),
            IsDismissed = false,
            CreatedAt = now,
            LastModifiedAt = now
        };

        // 4. Atomically create reminder AND update task's reminderId in single transaction
        // Uses Cosmos DB TransactionalBatch to ensure both operations succeed or both fail
        var createdReminder = await _reminderRepository.CreateWithTaskLinkAsync(reminder, taskId);

        _logger.LogInformation("Created reminder {ReminderId} for task {TaskId} by user {UserId}",
            createdReminder.Id, taskId, userId);

        return createdReminder;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TaskReminderDocument>> GetRemindersAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
        }

        _logger.LogDebug("Getting reminders for user {UserId}", userId);

        return await _reminderRepository.GetByUserIdAsync(userId);
    }

    /// <inheritdoc />
    public async Task<TaskReminderDocument?> GetReminderByIdAsync(string userId, string reminderId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(reminderId))
        {
            throw new ArgumentException("Reminder ID cannot be empty", nameof(reminderId));
        }

        _logger.LogDebug("Getting reminder {ReminderId} for user {UserId}", reminderId, userId);

        return await _reminderRepository.GetByIdAsync(userId, reminderId);
    }

    /// <inheritdoc />
    public async Task<TaskReminderDocument> SnoozeAsync(string userId, string reminderId, DateTime newScheduledTime)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(reminderId))
        {
            throw new ArgumentException("Reminder ID cannot be empty", nameof(reminderId));
        }

        _logger.LogDebug("Snoozing reminder {ReminderId} to {NewScheduledTime} for user {UserId}",
            reminderId, newScheduledTime, userId);

        // 1. Load reminder
        var reminder = await _reminderRepository.GetByIdAsync(userId, reminderId);
        if (reminder is null)
        {
            throw new ReminderNotFoundException(reminderId);
        }

        // 2. Check if already dismissed
        if (reminder.IsDismissed)
        {
            throw new ReminderAlreadyDismissedException(reminderId);
        }

        // Note: We intentionally do NOT validate scheduledTime is in the future here.
        // This operation may be called via delayed sync from a mobile client, where the
        // snooze was valid when performed but the scheduled time has since passed.
        // The mobile client validates future time at the moment of user interaction.

        // 3. Update scheduledTime and LastModifiedAt
        var now = DateTime.UtcNow;
        reminder.ScheduledTime = DateTime.SpecifyKind(newScheduledTime, DateTimeKind.Utc);
        reminder.LastModifiedAt = now;

        // 5. Save and return
        var updatedReminder = await _reminderRepository.UpdateAsync(reminder);

        _logger.LogInformation("Snoozed reminder {ReminderId} to {NewScheduledTime} for user {UserId}",
            reminderId, newScheduledTime, userId);

        return updatedReminder;
    }

    /// <inheritdoc />
    /// <remarks>
    /// This operation is idempotent - dismissing an already-dismissed reminder
    /// succeeds without throwing an exception or making any state changes.
    /// </remarks>
    public async Task DismissAsync(string userId, string reminderId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(reminderId))
        {
            throw new ArgumentException("Reminder ID cannot be empty", nameof(reminderId));
        }

        _logger.LogDebug("Dismissing reminder {ReminderId} for user {UserId}", reminderId, userId);

        // 1. Load reminder
        var reminder = await _reminderRepository.GetByIdAsync(userId, reminderId);
        if (reminder is null)
        {
            throw new ReminderNotFoundException(reminderId);
        }

        // 2. Idempotent: already dismissed is success (no-op)
        if (reminder.IsDismissed)
        {
            _logger.LogDebug("Reminder {ReminderId} already dismissed, no-op (idempotent)", reminderId);
            return;
        }

        // 3. Mark as dismissed
        var now = DateTime.UtcNow;
        reminder.IsDismissed = true;
        reminder.DismissedAt = now;
        reminder.LastModifiedAt = now;

        // 4. Save - single document update, no batch needed (task unchanged)
        await _reminderRepository.UpdateAsync(reminder);

        _logger.LogInformation("Dismissed reminder {ReminderId} for user {UserId}", reminderId, userId);
    }
}
