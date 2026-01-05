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
        var task = await _taskRepository.GetByIdAsync(taskId, userId);
        if (task is null)
        {
            throw new TaskNotFoundException(taskId);
        }

        // 2. Check task doesn't already have a reminder
        var existingReminder = await _reminderRepository.GetByTaskIdAsync(taskId, userId);
        if (existingReminder is not null)
        {
            throw new ReminderAlreadyExistsException(taskId);
        }

        // 3. Create reminder with denormalized taskName
        var reminder = new TaskReminderDocument
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            TaskId = taskId,
            TaskName = task.Name,
            ScheduledTime = DateTime.SpecifyKind(scheduledTime, DateTimeKind.Utc),
            IsDismissed = false,
            CreatedAt = DateTime.UtcNow
        };

        // 4. Save reminder
        var createdReminder = await _reminderRepository.CreateAsync(reminder);

        // 5. Update task with reminderId (bidirectional link)
        await _taskRepository.UpdateReminderIdAsync(userId, taskId, reminder.Id);

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

        return await _reminderRepository.GetByIdAsync(reminderId, userId);
    }
}
