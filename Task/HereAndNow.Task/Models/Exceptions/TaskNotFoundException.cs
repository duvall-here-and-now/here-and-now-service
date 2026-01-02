namespace HereAndNowService.Models.Exceptions;

/// <summary>
/// Exception thrown when a requested task is not found
/// </summary>
public class TaskNotFoundException : Exception
{
    /// <summary>
    /// The ID of the task that was not found
    /// </summary>
    public string TaskId { get; }

    /// <summary>
    /// Creates a new TaskNotFoundException
    /// </summary>
    /// <param name="taskId">The ID of the task that was not found</param>
    public TaskNotFoundException(string taskId)
        : base($"Task with ID {taskId} not found")
    {
        TaskId = taskId;
    }

    /// <summary>
    /// Creates a new TaskNotFoundException with inner exception
    /// </summary>
    public TaskNotFoundException(string taskId, Exception innerException)
        : base($"Task with ID {taskId} not found", innerException)
    {
        TaskId = taskId;
    }
}
