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
    /// <param name="reminderId">The reminder ID</param>
    /// <param name="userId">The user ID (partition key)</param>
    /// <returns>The reminder document or null if not found</returns>
    Task<TaskReminderDocument?> GetByIdAsync(string reminderId, string userId);

    /// <summary>
    /// Gets a reminder by task ID to check if one already exists
    /// </summary>
    /// <param name="taskId">The task ID</param>
    /// <param name="userId">The user ID (partition key)</param>
    /// <returns>The reminder document or null if not found</returns>
    Task<TaskReminderDocument?> GetByTaskIdAsync(string taskId, string userId);

    /// <summary>
    /// Updates an existing reminder document
    /// </summary>
    /// <param name="reminder">The reminder document with updated values</param>
    /// <returns>The updated reminder document</returns>
    Task<TaskReminderDocument> UpdateAsync(TaskReminderDocument reminder);
}
