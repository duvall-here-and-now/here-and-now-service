namespace HereAndNowService.Models.Exceptions;

/// <summary>
/// Exception thrown when attempting to create a reminder for a task that already has one
/// </summary>
public class ReminderAlreadyExistsException : Exception
{
    /// <summary>
    /// The ID of the task that already has a reminder
    /// </summary>
    public string TaskId { get; }

    /// <summary>
    /// Creates a new ReminderAlreadyExistsException
    /// </summary>
    /// <param name="taskId">The ID of the task that already has a reminder</param>
    public ReminderAlreadyExistsException(string taskId)
        : base($"A reminder already exists for task with ID {taskId}")
    {
        TaskId = taskId;
    }

    /// <summary>
    /// Creates a new ReminderAlreadyExistsException with inner exception
    /// </summary>
    public ReminderAlreadyExistsException(string taskId, Exception innerException)
        : base($"A reminder already exists for task with ID {taskId}", innerException)
    {
        TaskId = taskId;
    }
}
