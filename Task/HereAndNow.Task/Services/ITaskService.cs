using HereAndNowService.Models;

namespace HereAndNowService.Services;

/// <summary>
/// Service interface for Task business logic operations
/// </summary>
public interface ITaskService
{
    /// <summary>
    /// Creates a new task for the specified user
    /// </summary>
    /// <param name="name">The name of the task</param>
    /// <param name="userId">The ID of the user creating the task</param>
    /// <returns>The created task document</returns>
    Task<TaskDocument> CreateTaskAsync(string name, string userId);

    /// <summary>
    /// Gets all tasks for a user, optionally filtered by state
    /// </summary>
    /// <param name="userId">The ID of the user</param>
    /// <param name="state">Optional state filter (OnDeck, InProgress, Completed)</param>
    /// <returns>List of tasks belonging to the user</returns>
    Task<IEnumerable<TaskDocument>> GetTasksAsync(string userId, string? state = null);

    /// <summary>
    /// Gets tasks for a user with sorting and pagination
    /// </summary>
    /// <param name="userId">The ID of the user</param>
    /// <param name="state">Optional state filter (OnDeck, InProgress, Completed)</param>
    /// <param name="orderBy">Field to order by (createdAt or completedAt)</param>
    /// <param name="direction">Sort direction (asc or desc)</param>
    /// <param name="skip">Number of items to skip</param>
    /// <param name="take">Number of items to return (max 100)</param>
    /// <returns>Paginated result with items, total count, and hasMore flag</returns>
    Task<PagedResult<TaskDocument>> GetTasksPagedAsync(
        string userId,
        string? state = null,
        string orderBy = "createdAt",
        string direction = "asc",
        int skip = 0,
        int take = 50);

    /// <summary>
    /// Gets a specific task by ID for a user
    /// </summary>
    /// <param name="taskId">The task ID</param>
    /// <param name="userId">The user ID</param>
    /// <returns>The task document</returns>
    /// <exception cref="Models.Exceptions.TaskNotFoundException">Thrown when task is not found</exception>
    Task<TaskDocument> GetTaskByIdAsync(string taskId, string userId);

    /// <summary>
    /// Updates a task with the provided changes
    /// </summary>
    /// <param name="taskId">The task ID</param>
    /// <param name="userId">The user ID</param>
    /// <param name="name">The new name (null to keep existing)</param>
    /// <param name="state">The new state (null to keep existing)</param>
    /// <returns>The updated task document</returns>
    /// <exception cref="Models.Exceptions.TaskNotFoundException">Thrown when task is not found</exception>
    Task<TaskDocument> UpdateTaskAsync(string taskId, string userId, string? name, string? state);

    /// <summary>
    /// Creates a new task with an optional reminder in a single operation.
    /// If scheduledTime is provided, creates both the task and its reminder.
    /// </summary>
    /// <param name="name">The name of the task</param>
    /// <param name="userId">The ID of the user creating the task</param>
    /// <param name="scheduledTime">Optional UTC time for the reminder. If null, only task is created.</param>
    /// <returns>The created task document with reminderId populated if reminder was created</returns>
    Task<TaskDocument> CreateTaskWithOptionalReminderAsync(string name, string userId, DateTime? scheduledTime);

    /// <summary>
    /// Completes a task with Unity - atomically updates the task to Completed state
    /// and dismisses the associated reminder (if any) in a single transactional batch.
    /// This ensures data consistency: either both updates succeed or neither does.
    /// </summary>
    /// <param name="userId">The user ID (partition key)</param>
    /// <param name="taskId">The task ID to complete</param>
    /// <returns>The completed task document</returns>
    /// <exception cref="Models.Exceptions.TaskNotFoundException">Thrown when task is not found</exception>
    /// <exception cref="Models.Exceptions.UnityTransactionFailedException">Thrown when the transactional batch fails</exception>
    Task<TaskDocument> CompleteTaskWithUnityAsync(string userId, string taskId);
}
