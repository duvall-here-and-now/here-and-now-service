using HereAndNowService.Models;

namespace HereAndNowService.Repositories;

/// <summary>
/// Repository interface for TaskReminder document persistence operations
/// </summary>
public interface ITaskReminderRepository
{
    /// <summary>
    /// Creates a new reminder document in Cosmos DB
    /// </summary>
    /// <param name="reminder">The reminder document to create</param>
    /// <returns>The created reminder document with generated ID</returns>
    Task<TaskReminderDocument> CreateAsync(TaskReminderDocument reminder);

    /// <summary>
    /// Gets all non-dismissed reminders for a user, sorted by scheduled time ascending
    /// </summary>
    /// <param name="userId">The user ID (partition key)</param>
    /// <returns>List of non-dismissed reminders sorted by scheduledTime</returns>
    Task<IEnumerable<TaskReminderDocument>> GetByUserIdAsync(string userId);

    /// <summary>
    /// Gets a specific reminder by ID and user ID
    /// </summary>
    /// <param name="userId">The user ID (partition key)</param>
    /// <param name="reminderId">The reminder ID</param>
    /// <returns>The reminder document or null if not found</returns>
    Task<TaskReminderDocument?> GetByIdAsync(string userId, string reminderId);

    /// <summary>
    /// Gets a reminder by task ID to check if one already exists
    /// </summary>
    /// <param name="userId">The user ID (partition key)</param>
    /// <param name="taskId">The task ID</param>
    /// <returns>The reminder document or null if not found</returns>
    Task<TaskReminderDocument?> GetByTaskIdAsync(string userId, string taskId);

    /// <summary>
    /// Updates an existing reminder document
    /// </summary>
    /// <param name="reminder">The reminder document with updated values</param>
    /// <returns>The updated reminder document</returns>
    Task<TaskReminderDocument> UpdateAsync(TaskReminderDocument reminder);

    /// <summary>
    /// Creates a reminder and atomically updates the task's reminderId in a single transaction.
    /// Uses Cosmos DB TransactionalBatch to ensure both operations succeed or both fail.
    /// </summary>
    /// <param name="reminder">The reminder document to create</param>
    /// <param name="taskId">The task ID to link the reminder to</param>
    /// <returns>The created reminder document</returns>
    /// <exception cref="Models.Exceptions.TaskNotFoundException">If the task does not exist</exception>
    Task<TaskReminderDocument> CreateWithTaskLinkAsync(TaskReminderDocument reminder, string taskId);

    /// <summary>
    /// Checks if a reminder with the specified ID exists for the given user.
    /// Uses efficient point read to minimize RU cost.
    /// </summary>
    /// <param name="userId">The user ID (partition key)</param>
    /// <param name="reminderId">The reminder ID to check</param>
    /// <returns>True if the reminder exists, false otherwise</returns>
    Task<bool> ExistsAsync(string userId, string reminderId);
}
