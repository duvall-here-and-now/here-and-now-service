using HereAndNowService.Models;

namespace HereAndNowService.Repositories;

/// <summary>
/// Repository interface for Task document persistence operations
/// </summary>
public interface ITaskRepository
{
    /// <summary>
    /// Creates a new task document in Cosmos DB
    /// </summary>
    /// <param name="task">The task document to create</param>
    /// <returns>The created task document with generated ID</returns>
    Task<TaskDocument> CreateAsync(TaskDocument task);

    /// <summary>
    /// Gets all tasks for a specific user, optionally filtered by state
    /// </summary>
    /// <param name="userId">The user ID (partition key)</param>
    /// <param name="state">Optional state filter</param>
    /// <returns>List of tasks belonging to the user</returns>
    Task<IEnumerable<TaskDocument>> GetByUserIdAsync(string userId, string? state = null);

    /// <summary>
    /// Gets tasks for a specific user with sorting and pagination.
    /// Note: TotalCount may be approximate in high-concurrency scenarios due to
    /// separate queries for items and count.
    /// </summary>
    /// <param name="userId">The user ID (partition key)</param>
    /// <param name="state">Optional state filter</param>
    /// <param name="orderBy">Field to order by (createdAt or completedAt)</param>
    /// <param name="direction">Sort direction (asc or desc)</param>
    /// <param name="skip">Number of items to skip</param>
    /// <param name="take">Number of items to return (max 100)</param>
    /// <returns>Paginated result with items, total count, and hasMore flag</returns>
    Task<PagedResult<TaskDocument>> GetByUserIdPagedAsync(
        string userId,
        string? state = null,
        string orderBy = "createdAt",
        string direction = "asc",
        int skip = 0,
        int take = 50);

    /// <summary>
    /// Gets a specific task by ID and user ID
    /// </summary>
    /// <param name="taskId">The task ID</param>
    /// <param name="userId">The user ID (partition key)</param>
    /// <returns>The task document or null if not found</returns>
    Task<TaskDocument?> GetByIdAsync(string taskId, string userId);

    /// <summary>
    /// Updates an existing task document
    /// </summary>
    /// <param name="task">The task document with updated values</param>
    /// <returns>The updated task document</returns>
    Task<TaskDocument> UpdateAsync(TaskDocument task);

    /// <summary>
    /// Updates only the reminderId field on a task
    /// </summary>
    /// <param name="userId">The user ID (partition key)</param>
    /// <param name="taskId">The task ID</param>
    /// <param name="reminderId">The reminder ID to set (null to clear)</param>
    /// <returns>The updated task document</returns>
    Task<TaskDocument> UpdateReminderIdAsync(string userId, string taskId, string? reminderId);

    /// <summary>
    /// Atomically completes a task and dismisses its associated reminder using Cosmos DB transactional batch.
    /// This ensures both operations succeed or both fail - no partial state possible.
    /// </summary>
    /// <param name="task">The task document with updated state (Completed) and completedAt</param>
    /// <param name="reminder">The reminder document with updated isDismissed and dismissedAt (or null if no reminder)</param>
    /// <returns>The completed task document</returns>
    /// <exception cref="Models.Exceptions.UnityTransactionFailedException">If the transactional batch fails</exception>
    Task<TaskDocument> CompleteWithUnityAsync(TaskDocument task, TaskReminderDocument? reminder);
}
