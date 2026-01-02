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
    /// Deletes a task document (hard delete)
    /// </summary>
    /// <param name="taskId">The task ID</param>
    /// <param name="userId">The user ID (partition key)</param>
    Task DeleteAsync(string taskId, string userId);
}
