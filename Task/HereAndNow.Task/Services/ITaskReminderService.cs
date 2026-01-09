using HereAndNowService.Models;

namespace HereAndNowService.Services;

/// <summary>
/// Service interface for TaskReminder business logic operations
/// </summary>
public interface ITaskReminderService
{
    /// <summary>
    /// Creates a new reminder for a task
    /// </summary>
    /// <param name="userId">The ID of the user creating the reminder</param>
    /// <param name="taskId">The ID of the task to attach the reminder to</param>
    /// <param name="scheduledTime">UTC time when the reminder should trigger</param>
    /// <returns>The created reminder document</returns>
    /// <exception cref="Models.Exceptions.TaskNotFoundException">Thrown when task is not found</exception>
    /// <exception cref="Models.Exceptions.ReminderAlreadyExistsException">Thrown when task already has a reminder</exception>
    Task<TaskReminderDocument> CreateReminderAsync(string userId, string taskId, DateTime scheduledTime);

    /// <summary>
    /// Gets all non-dismissed reminders for a user, sorted by scheduled time
    /// </summary>
    /// <param name="userId">The ID of the user</param>
    /// <returns>List of reminders sorted by scheduledTime ascending</returns>
    Task<IEnumerable<TaskReminderDocument>> GetRemindersAsync(string userId);

    /// <summary>
    /// Gets a specific reminder by ID for a user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="reminderId">The reminder ID</param>
    /// <returns>The reminder document or null if not found</returns>
    Task<TaskReminderDocument?> GetReminderByIdAsync(string userId, string reminderId);

    /// <summary>
    /// Snoozes (reschedules) a reminder to a new time
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="reminderId">The reminder ID</param>
    /// <param name="newScheduledTime">The new scheduled time (must be in the future)</param>
    /// <returns>The updated reminder document</returns>
    /// <exception cref="Models.Exceptions.ReminderNotFoundException">Thrown when reminder is not found</exception>
    /// <exception cref="Models.Exceptions.ReminderAlreadyDismissedException">Thrown when reminder has been dismissed</exception>
    /// <exception cref="Models.Exceptions.InvalidScheduledTimeException">Thrown when scheduled time is in the past</exception>
    Task<TaskReminderDocument> SnoozeAsync(string userId, string reminderId, DateTime newScheduledTime);

    /// <summary>
    /// Dismisses a reminder without affecting the associated task
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="reminderId">The reminder ID</param>
    /// <returns>Task representing the async operation</returns>
    /// <exception cref="Models.Exceptions.ReminderNotFoundException">Thrown when reminder is not found</exception>
    /// <exception cref="Models.Exceptions.ReminderAlreadyDismissedException">Thrown when reminder has already been dismissed</exception>
    Task DismissAsync(string userId, string reminderId);
}
