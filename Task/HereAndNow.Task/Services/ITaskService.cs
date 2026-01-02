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
    /// Gets a specific task by ID for a user
    /// </summary>
    /// <param name="taskId">The task ID</param>
    /// <param name="userId">The user ID</param>
    /// <returns>The task document</returns>
    /// <exception cref="Models.Exceptions.TaskNotFoundException">Thrown when task is not found</exception>
    Task<TaskDocument> GetTaskByIdAsync(string taskId, string userId);
}
