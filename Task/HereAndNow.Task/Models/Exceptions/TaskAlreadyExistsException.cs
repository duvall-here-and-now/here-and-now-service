namespace HereAndNowService.Models.Exceptions;

/// <summary>
/// Exception thrown when attempting to create a task with an ID that already exists.
/// This typically occurs when a client retries a CreateTask command and the previous
/// attempt succeeded, or when there's an ID collision.
/// </summary>
public class TaskAlreadyExistsException : Exception
{
    /// <summary>
    /// The ID of the task that already exists
    /// </summary>
    public string TaskId { get; }

    /// <summary>
    /// Creates a new TaskAlreadyExistsException
    /// </summary>
    /// <param name="taskId">The ID of the task that already exists</param>
    public TaskAlreadyExistsException(string taskId)
        : base($"Task with ID {taskId} already exists")
    {
        TaskId = taskId;
    }

    /// <summary>
    /// Creates a new TaskAlreadyExistsException with inner exception
    /// </summary>
    public TaskAlreadyExistsException(string taskId, Exception innerException)
        : base($"Task with ID {taskId} already exists", innerException)
    {
        TaskId = taskId;
    }
}
