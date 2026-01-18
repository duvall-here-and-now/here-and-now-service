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
    /// Creates a new task with a client-generated ID.
    /// This enables optimistic UI patterns where the client can display the task
    /// immediately without waiting for server confirmation.
    /// </summary>
    /// <param name="userId">The ID of the user creating the task</param>
    /// <param name="taskId">The client-generated task ID (must be a valid GUID)</param>
    /// <param name="name">The name of the task</param>
    /// <returns>The created task document</returns>
    /// <exception cref="Models.Exceptions.TaskAlreadyExistsException">Thrown when a task with the given ID already exists</exception>
    Task<TaskDocument> CreateTaskWithIdAsync(string userId, string taskId, string name);

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

    /// <summary>
    /// Deletes a task with Unity - atomically soft-deletes the task (state = "Deleted")
    /// and dismisses the associated reminder (if any) in a single transactional batch.
    /// This ensures data consistency: either both updates succeed or neither does.
    /// </summary>
    /// <param name="userId">The user ID (partition key)</param>
    /// <param name="taskId">The task ID to delete</param>
    /// <exception cref="Models.Exceptions.TaskNotFoundException">Thrown when task is not found</exception>
    /// <exception cref="Models.Exceptions.UnityTransactionFailedException">Thrown when the transactional batch fails</exception>
    Task DeleteTaskWithUnityAsync(string userId, string taskId);

    /// <summary>
    /// Creates a new task and its associated reminder atomically with client-generated IDs.
    /// Both entities are created in a single transactional batch operation - either both
    /// succeed or both fail.
    /// </summary>
    /// <param name="userId">The user ID (partition key)</param>
    /// <param name="taskId">The client-generated task ID (must be a valid GUID)</param>
    /// <param name="taskReminderId">The client-generated reminder ID (must be a valid GUID)</param>
    /// <param name="name">The name of the task</param>
    /// <param name="scheduledTime">UTC time when the reminder should trigger</param>
    /// <returns>Tuple containing the created task and reminder documents</returns>
    /// <exception cref="Models.Exceptions.TaskAlreadyExistsException">Thrown when a task with the given ID already exists</exception>
    /// <exception cref="Models.Exceptions.TaskReminderAlreadyExistsException">Thrown when a reminder with the given ID already exists</exception>
    Task<(TaskDocument Task, TaskReminderDocument Reminder)> CreateTaskWithReminderAsync(
        string userId,
        string taskId,
        string taskReminderId,
        string name,
        DateTime scheduledTime);

    /// <summary>
    /// Updates the name of a task. If the task has an associated active reminder,
    /// the reminder's denormalized TaskName is also updated atomically.
    /// </summary>
    /// <param name="userId">The user ID (partition key)</param>
    /// <param name="taskId">The task ID to update</param>
    /// <param name="name">The new name for the task</param>
    /// <returns>The updated task document</returns>
    /// <exception cref="Models.Exceptions.TaskNotFoundException">Thrown when task is not found</exception>
    /// <exception cref="Models.Exceptions.InvalidStateTransitionException">Thrown when task is in Deleted state</exception>
    Task<TaskDocument> UpdateTaskNameAsync(string userId, string taskId, string name);

    /// <summary>
    /// Updates the state of a task. Handles completedAt auto-set/clear and Task-Reminder Unity.
    /// <list type="bullet">
    /// <item>Idempotent: transitioning to current state is a no-op success</item>
    /// <item>Deleted is terminal: cannot transition from Deleted to other states</item>
    /// <item>CompletedAt: auto-set when transitioning TO Completed, cleared when transitioning FROM Completed</item>
    /// <item>Unity: when transitioning to Completed or Deleted with a reminder, the reminder is atomically dismissed</item>
    /// </list>
    /// </summary>
    /// <param name="userId">The user ID (partition key)</param>
    /// <param name="taskId">The task ID to update</param>
    /// <param name="newState">The target state (OnDeck, InProgress, Completed, Deleted)</param>
    /// <returns>The updated task document</returns>
    /// <exception cref="Models.Exceptions.TaskNotFoundException">Thrown when task is not found</exception>
    /// <exception cref="Models.Exceptions.InvalidStateTransitionException">Thrown when attempting to transition from Deleted state</exception>
    /// <exception cref="ArgumentException">Thrown when newState is not a valid state value</exception>
    Task<TaskDocument> UpdateStateAsync(string userId, string taskId, string newState);
}
